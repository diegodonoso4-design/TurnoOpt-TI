using System.Net;
using System.Net.Sockets;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ApplicationDbContext context, ILogger<EmailService> logger)
        {
            _settings = settings.Value;
            _context = context;
            _logger = logger;
        }

        public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = ArmarMensaje(destinatario, asunto, cuerpoHtml);

            using var cliente = new SmtpClient();
            cliente.Timeout = 15000;

            await ConectarClienteAsync(cliente);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }

        public async Task NotificarMallaAprobadaAsync(int idMalla)
        {
            var malla = await _context.MallasCabecera
                .Include(m => m.Turnos)
                    .ThenInclude(t => t.Colaborador)
                .Include(m => m.Turnos)
                    .ThenInclude(t => t.TipoTurno)
                .FirstOrDefaultAsync(m => m.IdMalla == idMalla);

            if (malla == null || malla.Turnos == null || !malla.Turnos.Any())
                return;

            var turnosPorColab = malla.Turnos
                .GroupBy(t => t.Colaborador)
                .Where(g => g.Key != null && !string.IsNullOrWhiteSpace(g.Key.Email))
                .ToList();

            if (!turnosPorColab.Any())
                return;

            using var cliente = new SmtpClient();
            cliente.Timeout = 15000;

            try
            {
                // 1. Conexión y autenticación única nativa
                await ConectarClienteAsync(cliente);

                // 2. Envío en ráfaga reutilizando el socket abierto
                foreach (var grupo in turnosPorColab)
                {
                    var colab = grupo.Key!;
                    var turnosOrdenados = grupo.OrderBy(t => t.FechaTurno).ToList();
                    string cuerpoHtml = GenerarHtmlPlanificacion(malla, colab, turnosOrdenados);
                    string asunto = $"[TurnoOpt TI] Tu Malla de Turnos Oficial - {malla.PeriodoMes}/{malla.PeriodoAnio}";

                    try
                    {
                        var mensaje = ArmarMensaje(colab.Email, asunto, cuerpoHtml);
                        await cliente.SendAsync(mensaje);
                        _logger.LogInformation("Notificación enviada nativamente a {Email}", colab.Email);

                        await Task.Delay(250); // Pausa breve de cortesía
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al entregar correo a {Email}", colab.Email);
                    }
                }

                await cliente.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo crítico en la conexión SMTP nativa con {Host}:{Port}", _settings.SmtpServer, _settings.Port);
                throw;
            }
        }

        private async Task ConectarClienteAsync(SmtpClient cliente)
        {
            // Determinación estricta de TLS/SSL según el puerto estándar
            var socketOption = _settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            // Resolución explícita a IPv4 para evitar timeouts en redes de hosting que no soportan IPv6
            string hostFinal = _settings.SmtpServer;
            try
            {
                var ips = await Dns.GetHostAddressesAsync(_settings.SmtpServer);
                var ipV4 = ips.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                if (ipV4 != null)
                {
                    hostFinal = ipV4.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo forzar IPv4, utilizando nombre de host directo: {Host}", _settings.SmtpServer);
            }

            await cliente.ConnectAsync(hostFinal, _settings.Port, socketOption);
            await cliente.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
        }

        private MimeMessage ArmarMensaje(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;

            var builder = new BodyBuilder { HtmlBody = cuerpoHtml };
            mensaje.Body = builder.ToMessageBody();
            return mensaje;
        }

        private string GenerarHtmlPlanificacion(MallaCabecera malla, Colaborador colab, List<PlanificacionTurno> turnos)
        {
            var sb = new StringBuilder();
            sb.Append($@"
            <div style='font-family: Arial, sans-serif; max-width: 650px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #0f172a; border-bottom: 2px solid #2563eb; padding-bottom: 8px;'>Malla de Turnos Aprobada - {malla.PeriodoMes}/{malla.PeriodoAnio}</h2>
                <p>Hola <strong>{colab.Nombre} {colab.Apellido}</strong>,</p>
                <p>Se ha oficializado y aprobado la planificación de turnos para el período <strong>{malla.PeriodoMes}/{malla.PeriodoAnio}</strong>. A continuación, se detalla tu programación:</p>
                
                <table style='width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #f1f5f9; text-align: left;'>
                            <th style='padding: 10px; border: 1px solid #cbd5e1;'>Fecha</th>
                            <th style='padding: 10px; border: 1px solid #cbd5e1;'>Día</th>
                            <th style='padding: 10px; border: 1px solid #cbd5e1;'>Turno</th>
                            <th style='padding: 10px; border: 1px solid #cbd5e1;'>Horario</th>
                        </tr>
                    </thead>
                    <tbody>");

            foreach (var t in turnos)
            {
                string diaSemana = t.FechaTurno.ToString("dddd", new System.Globalization.CultureInfo("es-CL"));
                string nombreTurno = t.TipoTurno?.NombreTurno ?? t.TipoTurno?.CodigoTurno ?? "Turno";
                string horario = t.TipoTurno != null
                    ? $"{t.TipoTurno.HoraInicio:hh\\:mm} – {t.TipoTurno.HoraFin:hh\\:mm}"
                    : "Programado";

                sb.Append($@"
                        <tr>
                            <td style='padding: 8px; border: 1px solid #cbd5e1;'>{t.FechaTurno:dd/MM/yyyy}</td>
                            <td style='padding: 8px; border: 1px solid #cbd5e1; text-transform: capitalize;'>{diaSemana}</td>
                            <td style='padding: 8px; border: 1px solid #cbd5e1;'>{nombreTurno}</td>
                            <td style='padding: 8px; border: 1px solid #cbd5e1; font-weight: bold;'>{horario}</td>
                        </tr>");
            }

            sb.Append($@"
                    </tbody>
                </table>
                <p style='margin-top: 20px; font-size: 13px; color: #64748b;'>
                    Total de turnos asignados: <strong>{turnos.Count}</strong> ({turnos.Count * 8} horas estimadas).
                </p>
                <hr style='border: none; border-top: 1px solid #e2e8f0; margin-top: 25px;' />
                <p style='font-size: 12px; color: #94a3b8; text-align: center;'>TurnoOpt TI - Notificación Automatizada</p>
            </div>");

            return sb.ToString();
        }
    }
}