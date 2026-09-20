using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TurnoOptTI.Web.Data;
using TurnoOptTI.Web.Models;
using TurnoOptTI.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Registro de Controladores con Vistas
builder.Services.AddControllersWithViews();

// Conexión a MySQL mediante Pomelo
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Autenticación basada en Cookies para Roles (Supervisor y Operador)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

// Vinculación de configuración SMTP (appsettings.json -> EmailSettings)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Registro de servicios de la capa de negocio (Services)
builder.Services.AddScoped<ITurnoEngineService, TurnoEngineService>();
builder.Services.AddScoped<IRuleValidationService, RuleValidationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Manejo de entornos
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ejecuta siembra de datos al iniciar
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.InitializeAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocurrió un error al sembrar la base de datos.");
    }
}

app.Run();