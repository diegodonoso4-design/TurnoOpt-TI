using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text;
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
            await cliente.ConnectAsync(_settings.SmtpServer, _settings.Port, SecureSocketOptions.StartTls);
            await cliente.AuthenticateAsync(_settings.SenderEmail, _settings.Password);
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

            foreach (var grupo in turnosPorColab)
            {
                var colab = grupo.Key!;
                var turnosOrdenados = grupo.OrderBy(t => t.FechaTurno).ToList();
                string cuerpoHtml = GenerarHtmlPlanificacion(malla, colab, turnosOrdenados);
                string asunto = $"[TurnoOpt TI] Tu Malla de Turnos Oficial - {malla.PeriodoMes}/{malla.PeriodoAnio}";

                bool enviado = false;
                int intentos = 0;

                while (!enviado && intentos < 3)
                {
                    intentos++;
                    try
                    {
                        using var cliente = new SmtpClient();
                        await cliente.ConnectAsync(_settings.SmtpServer, _settings.Port, SecureSocketOptions.StartTls);
                        await cliente.AuthenticateAsync(_settings.SenderEmail, _settings.Password);

                        var mensaje = ArmarMensaje(colab.Email, asunto, cuerpoHtml);
                        await cliente.SendAsync(mensaje);
                        await cliente.DisconnectAsync(true);

                        enviado = true;
                        _logger.LogInformation("Notificación enviada con éxito a {Email}", colab.Email);
                    }
                    catch (SmtpCommandException ex) when (ex.Message.Contains("Too many emails"))
                    {
                        _logger.LogWarning("Límite de velocidad en Mailtrap para {Email}. Reintentando en 3s...", colab.Email);
                        await Task.Delay(3000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al entregar correo a {Email}", colab.Email);
                        break;
                    }
                }

                // Pausa prudente para el sandbox gratuito de Mailtrap
                await Task.Delay(2500);
            }
        }

        private MimeMessage ArmarMensaje(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_settings.SenderName, "notificaciones@turnoopt.cl"));
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