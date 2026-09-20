using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Services;
using TurnoOptTI.Web.ViewModels;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize(Roles = "Supervisor TI")]
    public class MallasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITurnoEngineService _turnoEngineService;
        private readonly IEmailService _emailService;

        public MallasController(
            ApplicationDbContext context,
            ITurnoEngineService turnoEngineService,
            IEmailService emailService)
        {
            _context = context;
            _turnoEngineService = turnoEngineService;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> Generar(int? idMalla, int? mes, int? anio)
        {
            int mesConsulta = mes ?? DateTime.Now.Month;
            int anioConsulta = anio ?? DateTime.Now.Year;

            var model = new GenerarMallaViewModel
            {
                EsquemasDisponibles = await _context.EsquemasCobertura
                    .Where(e => e.Estado == "Activo")
                    .ToListAsync(),
                Mes = mesConsulta,
                Anio = anioConsulta
            };

            // 1. Si viene por idMalla explícito (redirección inmediata tras presionar "Generar Malla")
            if (idMalla.HasValue)
            {
                model.MallaResultado = await _context.MallasCabecera
                    .Include(m => m.Esquema)
                    .Include(m => m.Turnos)
                        .ThenInclude(t => t.Colaborador)
                    .Include(m => m.Turnos)
                        .ThenInclude(t => t.TipoTurno)
                    .FirstOrDefaultAsync(m => m.IdMalla == idMalla.Value);
            }
            // 2. Si se consulta por período (al cambiar mes o año), cargar ÚNICAMENTE si está "Aprobada"
            else
            {
                model.MallaResultado = await _context.MallasCabecera
                    .Include(m => m.Esquema)
                    .Include(m => m.Turnos)
                        .ThenInclude(t => t.Colaborador)
                    .Include(m => m.Turnos)
                        .ThenInclude(t => t.TipoTurno)
                    .FirstOrDefaultAsync(m => m.PeriodoMes == mesConsulta
                                           && m.PeriodoAnio == anioConsulta
                                           && m.EstadoMalla == "Aprobada");
            }

            // Sincronizar los desplegables con el esquema de la malla cargada
            if (model.MallaResultado != null)
            {
                model.IdEsquema = model.MallaResultado.IdEsquema;
                model.Mes = model.MallaResultado.PeriodoMes;
                model.Anio = model.MallaResultado.PeriodoAnio;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generar(GenerarMallaViewModel model)
        {
            var supervisorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int supervisorId = int.Parse(supervisorIdStr ?? "1");

            try
            {
                var malla = await _turnoEngineService.GenerarMallaAsync(model.IdEsquema, model.Mes, model.Anio, supervisorId);
                return RedirectToAction("Generar", new { idMalla = malla.IdMalla });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null)
                {
                    inner = inner.InnerException;
                }

                ModelState.AddModelError(string.Empty, $"Error al generar malla: {inner.Message}");

                model.EsquemasDisponibles = await _context.EsquemasCobertura
                    .Where(e => e.Estado == "Activo")
                    .ToListAsync();

                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aprobar(int idMalla)
        {
            var malla = await _context.MallasCabecera
                .Include(m => m.Turnos)
                    .ThenInclude(t => t.Colaborador)
                .Include(m => m.Esquema)
                .FirstOrDefaultAsync(m => m.IdMalla == idMalla);

            if (malla == null)
            {
                return NotFound();
            }

            var supervisorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int supervisorId = int.Parse(supervisorIdStr ?? "1");

            malla.EstadoMalla = "Aprobada";
            malla.FechaAprobacion = DateTime.Now;
            malla.IdSupervisorAprueba = supervisorId;
            malla.FechaNotificacion = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notificación formal por correo electrónico vía SMTP con detalle individual de turnos
            try
            {
                await _emailService.NotificarMallaAprobadaAsync(idMalla);
                TempData["SuccessMsg"] = $"Malla #{malla.IdMalla} aprobada formalmente y notificaciones detalladas enviadas a los colaboradores.";
            }
            catch (Exception)
            {
                TempData["WarningMsg"] = $"La malla #{malla.IdMalla} fue aprobada con éxito, pero ocurrió una advertencia al despachar las notificaciones por correo. Verifique la configuración SMTP.";
            }

            return RedirectToAction("Generar", new { idMalla });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearEsquema(
            string nombreEsquema,
            string coberturaOperativa,
            int modalidadTurnos,
            int diasTrabajo,
            int diasDescanso,
            int dotacionPorTurno)
        {
            // Determinación automática del tipo de jornada según los turnos seleccionados
            string detalleTurnos = modalidadTurnos switch
            {
                3 => "3 turnos de 8h",
                2 => "2 turnos de 12h",
                _ => "1 turno diurno (8h)"
            };

            string tipoJornadaCalculado = $"{coberturaOperativa} ({detalleTurnos})";

            // Cálculo automatizado y seguro de la dotación mínima estructural (Equipos espejo / Factor de relevo)
            int dotacionMinimaFinal;
            if (coberturaOperativa == "L-S")
            {
                double factorSemanal = 6.0 / (diasTrabajo > 0 ? diasTrabajo : 5);
                dotacionMinimaFinal = (int)Math.Ceiling(dotacionPorTurno * modalidadTurnos * factorSemanal);
            }
            else
            {
                int totalCiclo = diasTrabajo + diasDescanso;
                double factorRelevo = (double)totalCiclo / (diasTrabajo > 0 ? diasTrabajo : 1);
                dotacionMinimaFinal = (int)Math.Ceiling(dotacionPorTurno * modalidadTurnos * factorRelevo);
            }

            var nuevoEsquema = new TurnoOptTI.Web.Models.EsquemaCobertura
            {
                NombreEsquema = nombreEsquema,
                TipoJornada = tipoJornadaCalculado,
                DiasTrabajoCiclo = diasTrabajo,
                DiasDescansoCiclo = diasDescanso,
                DotacionMinima = dotacionMinimaFinal,
                Estado = "Activo"
            };

            _context.EsquemasCobertura.Add(nuevoEsquema);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Esquema '{nombreEsquema}' creado exitosamente. Dotación mínima requerida: {dotacionMinimaFinal} colaboradores.";

            return RedirectToAction("Generar");
        }
    }
}