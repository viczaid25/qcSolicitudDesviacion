using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.Services;

namespace qcSolicitudDesviacion.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ActiveDirectoryService _ad;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public LoginModel(
        ActiveDirectoryService ad,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _ad = ad;
        _contextFactory = contextFactory;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        [Display(Name = "Usuario")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var adUser = _ad.Authenticate(Input.Username, Input.Password);

        if (adUser is null)
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña inválidos.");
            return Page();
        }

        var usuario = await ObtenerOCrearUsuarioAsync(adUser);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new("qc_usuario_id", usuario.Id.ToString()),
            new("pc_login_id", adUser.LoginId),
            new("username", usuario.UserName)
        };

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, usuario.Email));
        }

        if (!string.IsNullOrWhiteSpace(usuario.Departamento))
        {
            claims.Add(new Claim("department", usuario.Departamento));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!Url.IsLocalUrl(ReturnUrl))
        {
            ReturnUrl = "/";
        }

        return LocalRedirect(ReturnUrl!);
    }

    private async Task<QcDesvUsuario> ObtenerOCrearUsuarioAsync(AdUserInfo adUser)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var userName = adUser.LoginId.Trim().ToLowerInvariant();

        var email = string.IsNullOrWhiteSpace(adUser.Email)
            ? $"{userName}@sin-correo.local"
            : adUser.Email.Trim();

        var departamentoMeax = await ObtenerDepartamentoMeaxAsync(context, userName);

        var usuario = await context.QcDesvUsuarios
            .FirstOrDefaultAsync(x => x.UserName == userName);

        if (usuario is null)
        {
            usuario = new QcDesvUsuario
            {
                UserName = userName,
                NombreCompleto = string.IsNullOrWhiteSpace(adUser.DisplayName)
                    ? userName
                    : adUser.DisplayName.Trim(),
                Email = email,
                Departamento = departamentoMeax ?? adUser.Department,
                UnidadNegocio = null,
                Activo = true,
                FechaCreacion = DateTime.Now
            };

            context.QcDesvUsuarios.Add(usuario);
            await context.SaveChangesAsync();
        }
        else
        {
            usuario.NombreCompleto = string.IsNullOrWhiteSpace(adUser.DisplayName)
                ? usuario.NombreCompleto
                : adUser.DisplayName.Trim();

            usuario.Email = email;
            usuario.Departamento = departamentoMeax ?? adUser.Department;
            usuario.Activo = true;

            await context.SaveChangesAsync();
        }

        var tieneRolSolicitante = await context.QcDesvUsuarioRoles
            .AnyAsync(x => x.UsuarioId == usuario.Id && x.RolCodigo == "SOLICITANTE");

        if (!tieneRolSolicitante)
        {
            context.QcDesvUsuarioRoles.Add(new QcDesvUsuarioRol
            {
                UsuarioId = usuario.Id,
                RolCodigo = "SOLICITANTE",
                FechaAsignacion = DateTime.Now
            });

            await context.SaveChangesAsync();
        }

        return usuario;
    }

    private static async Task<string?> ObtenerDepartamentoMeaxAsync(
        ApplicationDbContext context,
        string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var loginNormalizado = login.Trim().ToLowerInvariant();

        return await context.Database
            .SqlQuery<string>($"""
                SELECT TOP (1)
                    LTRIM(RTRIM(CONVERT(nvarchar(150), [department]))) AS [Value]
                FROM [dbo].[meax_all_user]
                WHERE
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [department]))), '') IS NOT NULL
                    AND
                    (
                        LOWER(LTRIM(RTRIM(CONVERT(nvarchar(150), [pc_login_id])))) = {loginNormalizado}
                        OR LOWER(LTRIM(RTRIM(CONVERT(nvarchar(150), [username])))) = {loginNormalizado}
                    )
                """)
            .FirstOrDefaultAsync();
    }
}