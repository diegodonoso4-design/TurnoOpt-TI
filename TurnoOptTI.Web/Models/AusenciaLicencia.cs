using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class AusenciaLicencia
    {
        [Key]
        [Column("id_ausencia")]
        public int IdAusencia { get; set; }

        [Column("id_colaborador")]
        public int IdColaborador { get; set; }

        [Required]
        [MaxLength(25)]
        [Column("tipo_ausencia")]
        public string TipoAusencia { get; set; } = string.Empty;

        [Column("fecha_inicio", TypeName = "date")]
        public DateTime FechaInicio { get; set; }

        [Column("fecha_fin", TypeName = "date")]
        public DateTime FechaFin { get; set; }

        [Column("motivo_detalle")]
        public string? MotivoDetalle { get; set; }

        [Required]
        [MaxLength(15)]
        [Column("estado_aprobacion")]
        public string EstadoAprobacion { get; set; } = "Pendiente";

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [ForeignKey("IdColaborador")]
        public Colaborador? Colaborador { get; set; }
    }
}
