using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class EsquemaCobertura
    {
        [Key]
        [Column("id_esquema")]
        public int IdEsquema { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("nombre_esquema")]
        public string NombreEsquema { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Column("tipo_jornada")]
        public string TipoJornada { get; set; } = string.Empty;

        [Column("dias_trabajo_ciclo")]
        public int DiasTrabajoCiclo { get; set; }

        [Column("dias_descanso_ciclo")]
        public int DiasDescansoCiclo { get; set; }

        [Column("dotacion_minima")]
        public int DotacionMinima { get; set; }

        [Required]
        [MaxLength(10)]
        [Column("estado")]
        public string Estado { get; set; } = "Activo";

        public ICollection<MallaCabecera> Mallas { get; set; } = new List<MallaCabecera>();
    }
}
