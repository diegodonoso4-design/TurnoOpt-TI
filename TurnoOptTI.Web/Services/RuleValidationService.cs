using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;

namespace TurnoOptTI.Web.Services
{
    public class RuleValidationService : IRuleValidationService
    {
        private readonly ApplicationDbContext _context;

        public RuleValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool EsValido, string Mensaje)> ValidarDescansoInterjornadaAsync(int idColaborador, DateTime fechaTurno, int idTipoTurno)
        {
            var turnoNuevo = await _context.TiposTurno.FindAsync(idTipoTurno);
            if (turnoNuevo == null) return (false, "Tipo de turno no encontrado.");

            // 1. Verificar licencias o permisos vigentes
            var tieneAusencia = await _context.AusenciasLicencias
                .AnyAsync(a => a.IdColaborador == idColaborador
                            && a.FechaInicio <= fechaTurno.Date
                            && a.FechaFin >= fechaTurno.Date
                            && a.EstadoAprobacion != "Rechazada");

            if (tieneAusencia)
            {
                return (false, "Incompatible: El colaborador registra una licencia o permiso activo en esta fecha.");
            }

            // 2. Obtener parámetro de descanso mínimo (Art. 38 CT: 12h por defecto)
            var reglaDescanso = await _context.ReglasParametricas
                .FirstOrDefaultAsync(r => r.CodigoRegla == "DESC_MIN_INTERJORNADA" && r.FechaVigenciaFin == null);
            decimal horasMinimasDescanso = reglaDescanso?.ValorLimite ?? 12.00m;

            DateTime inicioTurnoNuevo = fechaTurno.Date.Add(turnoNuevo.HoraInicio);
            DateTime finTurnoNuevo = turnoNuevo.EsNocturno && turnoNuevo.HoraFin < turnoNuevo.HoraInicio
                ? fechaTurno.Date.AddDays(1).Add(turnoNuevo.HoraFin)
                : fechaTurno.Date.Add(turnoNuevo.HoraFin);

            // 3. Evaluar colisiones con turnos asignados en la ventana [-1 día, +1 día]
            var turnosVecinos = await _context.PlanificacionesTurno
                .Include(p => p.TipoTurno)
                .Where(p => p.IdColaborador == idColaborador
                         && p.FechaTurno >= fechaTurno.AddDays(-1)
                         && p.FechaTurno <= fechaTurno.AddDays(1)
                         && p.EstadoTurno != "Cancelado"
                         && p.EstadoTurno != "Ausente")
                .ToListAsync();

            foreach (var t in turnosVecinos)
            {
                if (t.TipoTurno == null) continue;

                DateTime inicioExistente = t.FechaTurno.Date.Add(t.TipoTurno.HoraInicio);
                DateTime finExistente = t.TipoTurno.EsNocturno && t.TipoTurno.HoraFin < t.TipoTurno.HoraInicio
                    ? t.FechaTurno.Date.AddDays(1).Add(t.TipoTurno.HoraFin)
                    : t.FechaTurno.Date.Add(t.TipoTurno.HoraFin);

                // Si el turno existente fue anterior al que se intenta programar
                if (inicioTurnoNuevo >= finExistente)
                {
                    var descanso = (inicioTurnoNuevo - finExistente).TotalHours;
                    if ((decimal)descanso < horasMinimasDescanso)
                    {
                        return (false, $"Infracción Art. 38 CT: Descanso interjornada insuficiente ({descanso:F1}h < {horasMinimasDescanso}h).");
                    }
                }

                // Si el turno existente es posterior al que se intenta programar
                if (inicioExistente >= finTurnoNuevo)
                {
                    var descanso = (inicioExistente - finTurnoNuevo).TotalHours;
                    if ((decimal)descanso < horasMinimasDescanso)
                    {
                        return (false, $"Infracción Art. 38 CT: Descanso posterior insuficiente ({descanso:F1}h < {horasMinimasDescanso}h).");
                    }
                }
            }

            return (true, "Cumple con el descanso legal interjornada.");
        }
    }
}