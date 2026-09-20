using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.ViewModels
{
    public class ReemplazoSugeridoViewModel
    {
        public int IdPlanificacion { get; set; }
        public PlanificacionTurno? TurnoAfectado { get; set; }
        public List<CandidatoReemplazo> CandidatosAptos { get; set; } = new();
        public List<PlanificacionTurno> TurnosVacantes { get; set; } = new();
        public List<ReemplazoHoraExtra> ReemplazosRealizados { get; set; } = new();
    }

    public class CandidatoReemplazo
    {
        public int IdColaborador { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public decimal HorasExtraAcumuladasMes { get; set; }
        public bool CumpleDescansoLegal { get; set; }
        public string MotivoIncompatibilidad { get; set; } = string.Empty;
    }
}