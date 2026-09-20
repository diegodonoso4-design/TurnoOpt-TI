using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;
using TurnoOptTI.Web.Services;
using TurnoOptTI.Web.ViewModels;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize(Roles = "Supervisor TI")]
    public class ReemplazosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IRuleValidationService _ruleValidator;

        public ReemplazosController(ApplicationDbContext context, IRuleValidationService ruleValidator)
        {
            _context = context;
            _ruleValidator = ruleValidator;
        }

        [HttpGet]
        public async Task<IActionResult> Asignar(int? idPlanificacion)
        {
            var model = new ReemplazoSugeridoViewModel
            {
                TurnosVacantes = await _context.PlanificacionesTurno
                    .Include(p => p.Colaborador)
                    .Include(p => p.TipoTurno)
                    .Where(p => p.EstadoTurno == "Ausente")
                    .OrderBy(p => p.FechaTurno)
                    .ToListAsync(),

                ReemplazosRealizados = await _context.ReemplazosHorasExtra
                    .Include(r => r.Planificacion).ThenInclude(p => p!.TipoTurno)
                    .Include(r => r.ColaboradorReemplazo)
                    .Include(r => r.SupervisorAsigna)
                    .OrderByDescending(r => r.FechaAsignacion)
                    .Take(10)
                    .ToListAsync()
            };

            if (idPlanificacion.HasValue)
            {
                model.IdPlanificacion = idPlanificacion.Value;
                model.TurnoAfectado = await _context.PlanificacionesTurno
                    .Include(p => p.Colaborador)
                    .Include(p => p.TipoTurno)
                    .FirstOrDefaultAsync(p => p.IdPlanificacion == idPlanificacion.Value);

                if (model.TurnoAfectado != null)
                {
                    var operadores = await _context.Colaboradores
                        .Where(c => c.IdRol == 2 && c.Estado == "Activo" && c.IdColaborador != model.TurnoAfectado.IdColaborador)
                        .ToListAsync();

                    foreach (var op in operadores)
                    {
                        var (esValido, mensaje) = await _ruleValidator.ValidarDescansoInterjornadaAsync(
                            op.IdColaborador, model.TurnoAfectado.FechaTurno, model.TurnoAfectado.IdTipoTurno);

                        var totalHorasExtra = await _context.ReemplazosHorasExtra
                            .Where(r => r.IdColaboradorReemplazo == op.IdColaborador && r.FechaAsignacion.Month == DateTime.Now.Month)
                            .SumAsync(r => (decimal?)r.HorasExtraAutorizadas) ?? 0;

                        model.CandidatosAptos.Add(new CandidatoReemplazo
                        {
                            IdColaborador = op.IdColaborador,
                            NombreCompleto = $"{op.Nombre} {op.Apellido}",
                            Cargo = op.CargoTi,
                            HorasExtraAcumuladasMes = totalHorasExtra,
                            CumpleDescansoLegal = esValido,
                            MotivoIncompatibilidad = esValido ? "Disponible" : mensaje
                        });
                    }

                    // Ordenar por cumplimiento legal y menor cantidad de horas extras acumuladas (equidad)
                    model.CandidatosAptos = model.CandidatosAptos
                        .OrderByDescending(c => c.CumpleDescansoLegal)
                        .ThenBy(c => c.HorasExtraAcumuladasMes)
                        .ToList();
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarReemplazo(int idPlanificacion, int idColaboradorSuplente, decimal horasExtra)
        {
            var turno = await _context.PlanificacionesTurno
                .Include(p => p.TipoTurno)
                .FirstOrDefaultAsync(p => p.IdPlanificacion == idPlanificacion);

            if (turno == null) return NotFound();

            var supervisorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int idSupervisor = int.Parse(supervisorIdStr ?? "1");

            // Buscar la ausencia asociada a este colaborador y fecha
            var ausencia = await _context.AusenciasLicencias
                .FirstOrDefaultAsync(a => a.IdColaborador == turno.IdColaborador
                                       && turno.FechaTurno >= a.FechaInicio
                                       && turno.FechaTurno <= a.FechaFin);

            // Transacción ACID explícita (CP09)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reemplazo = new ReemplazoHoraExtra
                {
                    IdPlanificacion = idPlanificacion,
                    IdColaboradorReemplazo = idColaboradorSuplente,
                    IdAusencia = ausencia?.IdAusencia ?? 1,
                    IdSupervisorAsigna = idSupervisor,
                    HorasExtraAutorizadas = horasExtra,
                    FechaAsignacion = DateTime.Now,
                    Estado = "Confirmado"
                };

                await _context.ReemplazosHorasExtra.AddAsync(reemplazo);

                // Modificar estado del turno original
                turno.EstadoTurno = "Reemplazado";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMsg"] = "Reemplazo transaccional formalizado con éxito en la base de datos.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMsg"] = $"Fallo al procesar el reemplazo: {ex.Message}";
            }

            return RedirectToAction(nameof(Asignar));
        }
    }
}