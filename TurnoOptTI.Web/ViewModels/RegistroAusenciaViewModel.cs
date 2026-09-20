using System.ComponentModel.DataAnnotations;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.ViewModels
{
    public class RegistroAusenciaViewModel
    {
        [Required(ErrorMessage = "Debe seleccionar un colaborador.")]
        [Display(Name = "Colaborador")]
        public int IdColaborador { get; set; }

        [Required(ErrorMessage = "Seleccione el tipo de ausencia.")]
        [Display(Name = "Tipo de Ausencia")]
        public string TipoAusencia { get; set; } = "Licencia Médica";

        [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Inicio")]
        public DateTime FechaInicio { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "La fecha de término es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Término")]
        public DateTime FechaFin { get; set; } = DateTime.Today.AddDays(3);

        [Display(Name = "Motivo / Folio Médico")]
        [StringLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres.")]
        public string? MotivoDetalle { get; set; }

        // Listas auxiliares para la interfaz
        public List<Colaborador> ColaboradoresDisponibles { get; set; } = new();
        public List<AusenciaLicencia> AusenciasRecientes { get; set; } = new();
    }
}