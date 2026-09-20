namespace TurnoOptTI.Web.Services
{
    public interface IEmailService
    {
        Task EnviarCorreoAsync(string destinatario, string asunto, string cuerpoHtml);
        Task NotificarMallaAprobadaAsync(int idMalla);
    }
}