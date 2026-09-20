using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Rol> Roles => Set<Rol>();
        public DbSet<Colaborador> Colaboradores => Set<Colaborador>();
        public DbSet<ReglaParametrica> ReglasParametricas => Set<ReglaParametrica>();
        public DbSet<EsquemaCobertura> EsquemasCobertura => Set<EsquemaCobertura>();
        public DbSet<TipoTurno> TiposTurno => Set<TipoTurno>();
        public DbSet<MallaCabecera> MallasCabecera => Set<MallaCabecera>();
        public DbSet<PlanificacionTurno> PlanificacionesTurno => Set<PlanificacionTurno>();
        public DbSet<AusenciaLicencia> AusenciasLicencias => Set<AusenciaLicencia>();
        public DbSet<ReemplazoHoraExtra> ReemplazosHorasExtra => Set<ReemplazoHoraExtra>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeo explícito de nombres de tablas físicas
            modelBuilder.Entity<Rol>().ToTable("roles");
            modelBuilder.Entity<Colaborador>().ToTable("colaboradores");
            modelBuilder.Entity<ReglaParametrica>().ToTable("reglas_parametricas");
            modelBuilder.Entity<EsquemaCobertura>().ToTable("esquemas_cobertura");
            modelBuilder.Entity<TipoTurno>().ToTable("tipos_turno");
            modelBuilder.Entity<MallaCabecera>().ToTable("mallas_cabecera");
            modelBuilder.Entity<PlanificacionTurno>().ToTable("planificacion_turnos");
            modelBuilder.Entity<AusenciaLicencia>().ToTable("ausencias_licencias");
            modelBuilder.Entity<ReemplazoHoraExtra>().ToTable("reemplazos_horas_extra");

            // Índices únicos
            modelBuilder.Entity<Colaborador>()
                .HasIndex(c => c.Rut).IsUnique();

            modelBuilder.Entity<Colaborador>()
                .HasIndex(c => c.Email).IsUnique();

            modelBuilder.Entity<ReglaParametrica>()
                .HasIndex(r => r.CodigoRegla).IsUnique();

            modelBuilder.Entity<TipoTurno>()
                .HasIndex(t => t.CodigoTurno).IsUnique();

            modelBuilder.Entity<MallaCabecera>()
                .HasIndex(m => new { m.IdEsquema, m.PeriodoMes, m.PeriodoAnio }).IsUnique();

            modelBuilder.Entity<PlanificacionTurno>()
                .HasIndex(p => new { p.IdColaborador, p.FechaTurno }).IsUnique();

            modelBuilder.Entity<ReemplazoHoraExtra>()
                .HasIndex(r => r.IdPlanificacion).IsUnique();
        }
    }
}