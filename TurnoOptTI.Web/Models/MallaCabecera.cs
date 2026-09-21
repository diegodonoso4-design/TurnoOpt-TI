using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class MallaCabecera
    {
        [Key]
        [Column("id_malla")]
        public int IdMalla { get; set; }

        [Column("id_esquema")]
        public int IdEsquema { get; set; }

        [Column("id_supervisor_crea")]
        public int IdSupervisorCrea { get; set; }

        [Column("id_supervisor_aprueba")]
        public int? IdSupervisorAprueba { get; set; }

        [Column("periodo_mes")]
        public int PeriodoMes { get; set; }

        [Column("periodo_anio")]
        public int PeriodoAnio { get; set; }

        [Required]
        [MaxLength(15)]
        [Column("estado_malla")]
        public string EstadoMalla { get; set; } = "Borrador";

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column("fecha_aprobacion")]
        public DateTime? FechaAprobacion { get; set; }

        [Column("fecha_notificacion")]
        public DateTime? FechaNotificacion { get; set; }

        [ForeignKey("IdEsquema")]
        public EsquemaCobertura? Esquema { get; set; }

       [ForeignKey("IdSupervisorCrea")]
        public Colaborador? SupervisorCrea { get; set; }

        [ForeignKey("IdSupervisorAprueba")]
        public Colaborador? SupervisorAprueba { get; set; }

        public ICollection<PlanificacionTurno> Turnos { get; set; } = new List<PlanificacionTurno>();
    }
}