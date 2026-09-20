using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize(Roles = "Supervisor TI")]
    public class ReglasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReglasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var reglas = await _context.ReglasParametricas
                .OrderBy(r => r.IdRegla)
                .ToListAsync();

            return View(reglas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Actualizar(int idRegla, decimal valorLimite)
        {
            var regla = await _context.ReglasParametricas.FindAsync(idRegla);
            if (regla == null)
            {
                TempData["ErrorMsg"] = "La regla paramétrica especificada no existe.";
                return RedirectToAction(nameof(Index));
            }

            var supervisorIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int supervisorId = int.Parse(supervisorIdStr ?? "1");

            regla.ValorLimite = valorLimite;
            regla.IdSupervisorActualiza = supervisorId;
            regla.FechaVigenciaInicio = DateTime.Today;

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Regla '{regla.NombreRegla}' actualizada exitosamente a {valorLimite:F2} {regla.UnidadMedida}.";
            return RedirectToAction(nameof(Index));
        }
    }
}