using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
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

builder.Services.Configure<ActiveDirectoryOptions>(
    builder.Configuration.GetSection("ActiveDirectory"));

builder.Services.AddScoped<ActiveDirectoryService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "qcSolicitudDesviacion.Auth";
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<QcDesvWorkflowService>();
builder.Services.AddScoped<SolicitudDesviacionService>();
builder.Services.AddScoped<AprobacionService>();
builder.Services.AddScoped<ExtensionService>();
builder.Services.AddScoped<CierreDesviacionService>();
builder.Services.AddScoped<ArchivoService>();
builder.Services.AddScoped<UsuarioActualService>();
builder.Services.AddScoped<CatalogoService>();
builder.Services.AddScoped<AdminAprobadoresService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

app.Run();