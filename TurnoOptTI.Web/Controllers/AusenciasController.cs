using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;
using TurnoOptTI.Web.ViewModels;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize]
    public class AusenciasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AusenciasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Crear()
        {
            var model = new RegistroAusenciaViewModel();
            await CargarDatosAuxiliares(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(RegistroAusenciaViewModel model)
        {
            if (model.FechaFin < model.FechaInicio)
            {
                ModelState.AddModelError("FechaFin", "La fecha de término no puede ser anterior a la fecha de inicio.");
            }

            if (!ModelState.IsValid)
            {
                await CargarDatosAuxiliares(model);
                return View(model);
            }

            // Transacción para guardar ausencia y actualizar estado de turnos (ACID)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var nuevaAusencia = new AusenciaLicencia
                {
                    IdColaborador = model.IdColaborador,
                    TipoAusencia = model.TipoAusencia,
                    FechaInicio = model.FechaInicio,
                    FechaFin = model.FechaFin,
                    MotivoDetalle = model.MotivoDetalle,
                    EstadoAprobacion = "Aprobada",
                    FechaRegistro = DateTime.Now
                };

                await _context.AusenciasLicencias.AddAsync(nuevaAusencia);
                await _context.SaveChangesAsync();

                // Buscar turnos programados en el rango de fechas para marcarlos como 'Ausente'
                var turnosAfectados = await _context.PlanificacionesTurno
                    .Where(t => t.IdColaborador == model.IdColaborador
                             && t.FechaTurno >= model.FechaInicio
                             && t.FechaTurno <= model.FechaFin
                             && t.EstadoTurno == "Programado")
                    .ToListAsync();

                foreach (var turno in turnosAfectados)
                {
                    turno.EstadoTurno = "Ausente";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Ausencia registrada con éxito. Se actualizaron {turnosAfectados.Count} turnos a estado 'Ausente'.";
                return RedirectToAction(nameof(Crear));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, $"Error al registrar la ausencia: {ex.Message}");
                await CargarDatosAuxiliares(model);
                return View(model);
            }
        }

        private async Task CargarDatosAuxiliares(RegistroAusenciaViewModel model)
        {
            if (User.IsInRole("Supervisor TI"))
            {
                model.ColaboradoresDisponibles = await _context.Colaboradores
                    .Where(c => c.Estado == "Activo")
                    .OrderBy(c => c.Apellido)
                    .ToListAsync();

                model.AusenciasRecientes = await _context.AusenciasLicencias
                    .Include(a => a.Colaborador)
                    .OrderByDescending(a => a.FechaRegistro)
                    .Take(10)
                    .ToListAsync();
            }
            else
            {
                // Si es operador, solo puede seleccionarse a sí mismo
                var colabIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int colabId = int.Parse(colabIdStr ?? "0");

                model.IdColaborador = colabId;
                model.ColaboradoresDisponibles = await _context.Colaboradores
                    .Where(c => c.IdColaborador == colabId)
                    .ToListAsync();

                model.AusenciasRecientes = await _context.AusenciasLicencias
                    .Include(a => a.Colaborador)
                    .Where(a => a.IdColaborador == colabId)
                    .OrderByDescending(a => a.FechaRegistro)
                    .Take(10)
                    .ToListAsync();
            }
        }
    }
}