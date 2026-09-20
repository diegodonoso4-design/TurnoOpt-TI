using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class ReglaParametrica
    {
        [Key]
        [Column("id_regla")]
        public int IdRegla { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("codigo_regla")]
        public string CodigoRegla { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("nombre_regla")]
        public string NombreRegla { get; set; } = string.Empty;

        [Column("valor_limite", TypeName = "decimal(6,2)")]
        public decimal ValorLimite { get; set; }

        [MaxLength(20)]
        [Column("unidad_medida")]
        public string UnidadMedida { get; set; } = "Horas";

        [MaxLength(255)]
        [Column("fuente_normativa")]
        public string FuenteNormativa { get; set; } = string.Empty;

        [Column("fecha_vigencia_inicio", TypeName = "date")]
        public DateTime FechaVigenciaInicio { get; set; }

        [Column("fecha_vigencia_fin", TypeName = "date")]
        public DateTime? FechaVigenciaFin { get; set; }

        [Column("id_supervisor_actualiza")]
        public int? IdSupervisorActualiza { get; set; }
    }
}
