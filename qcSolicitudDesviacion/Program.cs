using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Auth;
using qcSolicitudDesviacion.Components;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseSqlServer(connectionString);
});

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddHttpContextAccessor();

builder.Services.AddCascadingAuthenticationState();

// Doble candado: el login de desarrollo solo existe si el entorno es
// Development Y Auth:DevLogin es true (en IIS el entorno es Production,
// así que el modo dev es imposible ahí aunque el flag se copie por error).
var devLoginActivo = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Auth:DevLogin");

builder.Services.AddSingleton(new AuthMode(devLoginActivo));

if (devLoginActivo)
{
    builder.Services.AddDevAuth();
}
else
{
    builder.Services.AddHubAuth(builder.Configuration);
}

// Toda la app requiere usuario autenticado, salvo [AllowAnonymous] explícito.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddScoped<UsuarioProvisioningService>();
builder.Services.AddScoped<QcDesvWorkflowService>();
builder.Services.AddScoped<SolicitudDesviacionService>();
builder.Services.AddScoped<AprobacionService>();
builder.Services.AddScoped<ExtensionService>();
builder.Services.AddScoped<CierreDesviacionService>();
builder.Services.AddScoped<ArchivoService>();
builder.Services.AddScoped<UsuarioActualService>();
builder.Services.AddScoped<CatalogoService>();
builder.Services.AddScoped<AdminAprobadoresService>();
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddHostedService<DeviationExpirationNotificationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();

if (!devLoginActivo)
{
    // Convierte el token SSO recibido por query (?meax_token=) en cookie de
    // sesión local y limpia la URL.
    app.UseHubSsoTokenBridge();
}

app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.Run();
