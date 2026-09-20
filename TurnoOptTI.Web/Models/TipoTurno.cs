using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class TipoTurno
    {
        [Key]
        [Column("id_tipo_turno")]
        public int IdTipoTurno { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("codigo_turno")]
        public string CodigoTurno { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("nombre_turno")]
        public string NombreTurno { get; set; } = string.Empty;

        [Column("hora_inicio")]
        public TimeSpan HoraInicio { get; set; }

        [Column("hora_fin")]
        public TimeSpan HoraFin { get; set; }

        [Column("duracion_horas", TypeName = "decimal(4,2)")]
        public decimal DuracionHoras { get; set; }

        [Column("es_nocturno")]
        public bool EsNocturno { get; set; }
    }
}
