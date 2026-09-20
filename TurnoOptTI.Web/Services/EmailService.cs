using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EmailService> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();

        public EmailService(IConfiguration configuration, ApplicationDbContext context, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            string apiKey = _configuration["EmailSettings:Password"]
                         ?? _configuration["EmailSettings__Password"]
                         ?? "";

            string remitenteEmail = _configuration["EmailSettings:SenderEmail"]
                                 ?? _configuration["EmailSettings__SenderEmail"]
                                 ?? "diegodonoso4@gmail.com";

            string remitenteNombre = _configuration["EmailSettings:SenderName"]
                                  ?? _configuration["EmailSettings__SenderName"]
                                  ?? "TurnoOpt TI - Notificaciones";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("API Key de SendGrid no configurada en EmailSettings:Password.");
                return;
            }

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = destinatario } }
                    }
                },
                from = new
                {
                    email = remitenteEmail,
                    name = remitenteNombre
                },
                subject = asunto,
                content = new[]
                {
                    new
                    {
                        type = "text/html",
                        value = cuerpoHtml
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Notificación enviada exitosamente vía API HTTPS a {Email}", destinatario);
            }
            else
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fallo al enviar correo vía API SendGrid: {StatusCode} - {Error}", response.StatusCode, errorBody);
            }
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

                try
                {
                    await EnviarCorreoAsync(colab.Email, asunto, cuerpoHtml);
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al despachar notificación a {Email}", colab.Email);
                }
            }
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