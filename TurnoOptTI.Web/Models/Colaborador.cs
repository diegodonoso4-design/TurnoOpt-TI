using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TurnoOptTI.Web.Models
{
    public class Colaborador
    {
        [Key]
        [Column("id_colaborador")]
        public int IdColaborador { get; set; }

        [Column("id_rol")]
        public int IdRol { get; set; }

        [Required]
        [MaxLength(12)]
        [Column("rut")]
        public string Rut { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("apellido")]
        public string Apellido { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [EmailAddress]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("cargo_ti")]
        public string CargoTi { get; set; } = string.Empty;

        [Column("horas_contrato_semanal")]
        public int HorasContratoSemanal { get; set; } = 40;

        [Required]
        [MaxLength(15)]
        [Column("estado")]
        public string Estado { get; set; } = "Activo";

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [ForeignKey("IdRol")]
        public Rol? Rol { get; set; }

        public ICollection<PlanificacionTurno> Planificaciones { get; set; } = new List<PlanificacionTurno>();
        public ICollection<AusenciaLicencia> Ausencias { get; set; } = new List<AusenciaLicencia>();
    }
}
