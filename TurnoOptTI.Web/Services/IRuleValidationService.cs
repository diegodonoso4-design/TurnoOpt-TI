namespace TurnoOptTI.Web.Services
{
    public interface IRuleValidationService
    {
        Task<(bool EsValido, string Mensaje)> ValidarDescansoInterjornadaAsync(int idColaborador, DateTime fechaTurno, int idTipoTurno);
    }
}