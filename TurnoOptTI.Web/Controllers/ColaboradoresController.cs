using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;

namespace TurnoOptTI.Web.Controllers
{
    [Authorize(Roles = "Supervisor TI")]
    public class ColaboradoresController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ColaboradoresController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var colaboradores = await _context.Colaboradores
                .Include(c => c.Rol)
                .OrderBy(c => c.Apellido)
                .ToListAsync();

            ViewBag.Roles = await _context.Roles.ToListAsync();
            return View(colaboradores);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Colaborador nuevoColaborador, string passwordPlana)
        {
            if (await _context.Colaboradores.AnyAsync(c => c.Rut == nuevoColaborador.Rut))
            {
                TempData["ErrorMsg"] = "Ya existe un colaborador registrado con ese RUT.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.Colaboradores.AnyAsync(c => c.Email == nuevoColaborador.Email))
            {
                TempData["ErrorMsg"] = "Ya existe un colaborador registrado con ese correo electrónico.";
                return RedirectToAction(nameof(Index));
            }

            // Encriptación BCrypt requerida por RNF03
            string passwordUsar = string.IsNullOrWhiteSpace(passwordPlana) ? "Operador2026!" : passwordPlana;
            nuevoColaborador.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordUsar);
            nuevoColaborador.FechaRegistro = DateTime.Now;
            nuevoColaborador.Estado = "Activo";

            _context.Colaboradores.Add(nuevoColaborador);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Colaborador {nuevoColaborador.Nombre} {nuevoColaborador.Apellido} registrado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idColaborador, string nuevoEstado)
        {
            var colab = await _context.Colaboradores.FindAsync(idColaborador);
            if (colab != null)
            {
                colab.Estado = nuevoEstado;
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Estado de {colab.Nombre} {colab.Apellido} actualizado a '{nuevoEstado}'.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetearPassword(int idColaborador, string? nuevaPassword)
        {
            var colaborador = await _context.Colaboradores.FindAsync(idColaborador);
            if (colaborador == null)
            {
                TempData["ErrorMsg"] = "El colaborador seleccionado no existe en la base de datos.";
                return RedirectToAction(nameof(Index));
            }

            // Si se deja vacío, se restablece a la clave por defecto
            string passwordFinal = string.IsNullOrWhiteSpace(nuevaPassword) ? "Operador2026!" : nuevaPassword.Trim();
            colaborador.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordFinal);

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Contraseña de {colaborador.Nombre} {colaborador.Apellido} restablecida exitosamente a: '{passwordFinal}'.";
            return RedirectToAction(nameof(Index));
        }
    }
}