using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.ViewModels;

namespace TurnoOptTI.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var colaborador = await _context.Colaboradores
                .Include(c => c.Rol)
                .FirstOrDefaultAsync(c => c.Email == model.Email);

            if (colaborador == null || !BCrypt.Net.BCrypt.Verify(model.Password, colaborador.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Credenciales incorrectas o usuario no encontrado.");
                return View(model);
            }

            if (colaborador.Estado != "Activo")
            {
                ModelState.AddModelError(string.Empty, "Su cuenta no se encuentra activa en el sistema.");
                return View(model);
            }

            // Crear Claims para la identidad y rol del usuario
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, colaborador.IdColaborador.ToString()),
                new Claim(ClaimTypes.Name, $"{colaborador.Nombre} {colaborador.Apellido}"),
                new Claim(ClaimTypes.Email, colaborador.Email),
                new Claim(ClaimTypes.Role, colaborador.Rol?.NombreRol ?? "Operador TI")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}