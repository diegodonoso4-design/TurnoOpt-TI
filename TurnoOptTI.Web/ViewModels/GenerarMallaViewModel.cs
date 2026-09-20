using System.ComponentModel.DataAnnotations;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.ViewModels
{
    public class GenerarMallaViewModel
    {
        [Required(ErrorMessage = "Seleccione el esquema de cobertura.")]
        [Display(Name = "Esquema de Cobertura")]
        public int IdEsquema { get; set; }

        [Required(ErrorMessage = "Seleccione el mes.")]
        [Range(1, 12, ErrorMessage = "Mes inválido.")]
        public int Mes { get; set; } = DateTime.Now.Month;

        [Required(ErrorMessage = "Seleccione el año.")]
        [Range(2026, 2030, ErrorMessage = "Año fuera de rango.")]
        public int Anio { get; set; } = 2026;

        public List<EsquemaCobertura> EsquemasDisponibles { get; set; } = new();
        public MallaCabecera? MallaResultado { get; set; }
    }
}