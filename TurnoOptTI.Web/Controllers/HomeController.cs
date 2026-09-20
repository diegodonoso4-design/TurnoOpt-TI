using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Supervisor TI"))
            {
                return View();
            }

            // Flujo para Operador TI: Obtener sus turnos del mes en curso
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int idOperador = int.Parse(idClaim ?? "0");

            int mesActual = DateTime.Now.Month;
            int anioActual = DateTime.Now.Year;

            var turnosOperador = await _context.PlanificacionesTurno
                .Include(p => p.TipoTurno)
                .Include(p => p.Malla).ThenInclude(m => m!.Esquema)
                .Where(p => p.IdColaborador == idOperador
                         && p.Malla!.EstadoMalla == "Aprobada" // <-- FILTRO CLAVE: Solo mallas aprobadas
                         && p.FechaTurno.Month == mesActual
                         && p.FechaTurno.Year == anioActual)
                .OrderBy(p => p.FechaTurno)
                .ToListAsync();

            return View(turnosOperador);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}