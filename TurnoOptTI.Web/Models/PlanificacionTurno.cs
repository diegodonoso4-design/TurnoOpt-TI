using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class PlanificacionTurno
    {
        [Key]
        [Column("id_planificacion")]
        public int IdPlanificacion { get; set; }

        [Column("id_malla")]
        public int IdMalla { get; set; }

        [Column("id_colaborador")]
        public int IdColaborador { get; set; }

        [Column("id_tipo_turno")]
        public int IdTipoTurno { get; set; }

        [Column("fecha_turno", TypeName = "date")]
        public DateTime FechaTurno { get; set; }

        [Column("es_hora_extra")]
        public bool EsHoraExtra { get; set; } = false;

        [Required]
        [MaxLength(15)]
        [Column("estado_turno")]
        public string EstadoTurno { get; set; } = "Programado";

        [ForeignKey("IdMalla")]
        public MallaCabecera? Malla { get; set; }

        [ForeignKey("IdColaborador")]
        public Colaborador? Colaborador { get; set; }

        [ForeignKey("IdTipoTurno")]
        public TipoTurno? TipoTurno { get; set; }

        public ReemplazoHoraExtra? Reemplazo { get; set; }
    }
}
