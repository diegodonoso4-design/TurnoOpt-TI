using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            // Asegura recrear las 9 tablas completas con el nuevo mapeo
            //await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            // Sembrar Roles
            if (!await context.Roles.AnyAsync())
            {
                await context.Roles.AddRangeAsync(
                    new Rol { IdRol = 1, NombreRol = "Supervisor TI", Descripcion = "Planificador y gestor operativo" },
                    new Rol { IdRol = 2, NombreRol = "Operador TI", Descripcion = "Personal técnico de guardias rotativas" }
                );
                await context.SaveChangesAsync();
            }

            // Sembrar Reglas Paramétricas (Ley 21.561 y Código del Trabajo)
            if (!await context.ReglasParametricas.AnyAsync())
            {
                await context.ReglasParametricas.AddRangeAsync(
                    new ReglaParametrica
                    {
                        CodigoRegla = "JORNADA_SEM_MAX",
                        NombreRegla = "Jornada Semanal Ordinaria Máxima",
                        ValorLimite = 40.00m,
                        UnidadMedida = "Horas",
                        FuenteNormativa = "Ley 21.561",
                        FechaVigenciaInicio = new DateTime(2026, 1, 1)
                    },
                    new ReglaParametrica
                    {
                        CodigoRegla = "DESC_MIN_INTERJORNADA",
                        NombreRegla = "Descanso Mínimo entre Jornadas",
                        ValorLimite = 12.00m,
                        UnidadMedida = "Horas",
                        FuenteNormativa = "Art. 38 Código del Trabajo",
                        FechaVigenciaInicio = new DateTime(2026, 1, 1)
                    },
                    new ReglaParametrica
                    {
                        CodigoRegla = "TOPE_HORAS_EXTRA_DIA",
                        NombreRegla = "Tope Máximo de Horas Extraordinarias Diarias",
                        ValorLimite = 2.00m,
                        UnidadMedida = "Horas",
                        FuenteNormativa = "Art. 31 Código del Trabajo",
                        FechaVigenciaInicio = new DateTime(2026, 1, 1)
                    }
                );
                await context.SaveChangesAsync();
            }

            // Sembrar Tipos de Turno (Actualizado con los 6 turnos de producción)
            if (!await context.TiposTurno.AnyAsync())
            {
                await context.TiposTurno.AddRangeAsync(
                    new TipoTurno
                    {
                        CodigoTurno = "T4X4-D",
                        NombreTurno = "Turno 4x4 Diurno (12h)",
                        HoraInicio = new TimeSpan(8, 0, 0),
                        HoraFin = new TimeSpan(20, 0, 0),
                        DuracionHoras = 12.00m,
                        EsNocturno = false
                    },
                    new TipoTurno
                    {
                        CodigoTurno = "T4X4-N",
                        NombreTurno = "Turno 4x4 Nocturno (12h)",
                        HoraInicio = new TimeSpan(20, 0, 0),
                        HoraFin = new TimeSpan(8, 0, 0),
                        DuracionHoras = 12.00m,
                        EsNocturno = true
                    },
                    new TipoTurno
                    {
                        CodigoTurno = "T5X2-D",
                        NombreTurno = "Turno 5x2 Oficina (8.5h)",
                        HoraInicio = new TimeSpan(8, 30, 0),
                        HoraFin = new TimeSpan(17, 30, 0),
                        DuracionHoras = 8.50m,
                        EsNocturno = false
                    },
                    new TipoTurno
                    {
                        CodigoTurno = "T5X2-M",
                        NombreTurno = "Turno 5x2 Mañana 8h",
                        HoraInicio = new TimeSpan(8, 0, 0),
                        HoraFin = new TimeSpan(16, 0, 0),
                        DuracionHoras = 8.00m,
                        EsNocturno = false
                    },
                    new TipoTurno
                    {
                        CodigoTurno = "T5X2-T",
                        NombreTurno = "Turno 5x2 Tarde 8h",
                        HoraInicio = new TimeSpan(16, 0, 0),
                        HoraFin = new TimeSpan(0, 0, 0),
                        DuracionHoras = 8.00m,
                        EsNocturno = false
                    },
                    new TipoTurno
                    {
                        CodigoTurno = "T5X2-N",
                        NombreTurno = "Turno 5x2 Noche 8h",
                        HoraInicio = new TimeSpan(0, 0, 0),
                        HoraFin = new TimeSpan(8, 0, 0),
                        DuracionHoras = 8.00m,
                        EsNocturno = true
                    }
                );
                await context.SaveChangesAsync();
            }

            // Sembrar Esquemas de Cobertura
            if (!await context.EsquemasCobertura.AnyAsync())
            {
                await context.EsquemasCobertura.AddRangeAsync(
                    new EsquemaCobertura
                    {
                        NombreEsquema = "Guardias Continuas NOC 24/7 (4x4)",
                        TipoJornada = "24/7 Continuo 4x4",
                        DiasTrabajoCiclo = 4,
                        DiasDescansoCiclo = 4,
                        DotacionMinima = 8,
                        Estado = "Activo"
                    },
                    new EsquemaCobertura
                    {
                        NombreEsquema = "Soporte Técnico Hábil (5x2)",
                        TipoJornada = "5x2 Oficina Diurno",
                        DiasTrabajoCiclo = 5,
                        DiasDescansoCiclo = 2,
                        DotacionMinima = 4,
                        Estado = "Activo"
                    }
                );
                await context.SaveChangesAsync();
            }

            // Sembrar Usuarios Iniciales con Contraseñas Encriptadas en BCrypt
            if (!await context.Colaboradores.AnyAsync())
            {
                await context.Colaboradores.AddRangeAsync(
                    new Colaborador
                    {
                        IdRol = 1,
                        Rut = "11.111.111-1",
                        Nombre = "Esteban",
                        Apellido = "Supervisor",
                        Email = "supervisor.ti@turnoopt.cl",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin2026!"),
                        CargoTi = "Supervisor General NOC",
                        HorasContratoSemanal = 40,
                        Estado = "Activo",
                        FechaRegistro = DateTime.Now
                    },
                    new Colaborador
                    {
                        IdRol = 2,
                        Rut = "22.222.222-2",
                        Nombre = "Diego",
                        Apellido = "Operador",
                        Email = "operador.ti@turnoopt.cl",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operador2026!"),
                        CargoTi = "Operador de Infraestructura TI",
                        HorasContratoSemanal = 40,
                        Estado = "Activo",
                        FechaRegistro = DateTime.Now
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}