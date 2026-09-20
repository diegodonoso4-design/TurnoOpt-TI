using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class Rol
    {
        [Key]
        [Column("id_rol")]
        public int IdRol { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("nombre_rol")]
        public string NombreRol { get; set; } = string.Empty;

        [MaxLength(255)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        public ICollection<Colaborador> Colaboradores { get; set; } = new List<Colaborador>();
    }
}
