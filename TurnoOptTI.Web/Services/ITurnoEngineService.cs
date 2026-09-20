using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Services
{
    public interface ITurnoEngineService
    {

        Task<MallaCabecera> GenerarMallaAsync(int idEsquema, int mes, int anio, int idSupervisorCrea);
    }
}