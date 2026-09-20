using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class ReemplazoHoraExtra
    {
        [Key]
        [Column("id_reemplazo")]
        public int IdReemplazo { get; set; }

        [Column("id_planificacion")]
        public int IdPlanificacion { get; set; }

        [Column("id_colaborador_reemplazo")]
        public int IdColaboradorReemplazo { get; set; }

        [Column("id_ausencia")]
        public int IdAusencia { get; set; }

        [Column("id_supervisor_asigna")]
        public int IdSupervisorAsigna { get; set; }

        [Column("horas_extra_autorizadas", TypeName = "decimal(4,2)")]
        public decimal HorasExtraAutorizadas { get; set; }

        [Column("fecha_asignacion")]
        public DateTime FechaAsignacion { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(15)]
        [Column("estado")]
        public string Estado { get; set; } = "Confirmado";

        [ForeignKey("IdPlanificacion")]
        public PlanificacionTurno? Planificacion { get; set; }

        [ForeignKey("IdColaboradorReemplazo")]
        public Colaborador? ColaboradorReemplazo { get; set; }

        [ForeignKey("IdAusencia")]
        public AusenciaLicencia? Ausencia { get; set; }

        [ForeignKey("IdSupervisorAsigna")]
        public Colaborador? SupervisorAsigna { get; set; }
    }
}
