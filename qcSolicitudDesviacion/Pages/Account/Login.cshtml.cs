using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using qcSolicitudDesviacion.Auth;
using qcSolicitudDesviacion.Services;

namespace qcSolicitudDesviacion.Pages.Account;

/// <summary>
/// Login SOLO para modo desarrollo (sin Meax One). En producción esta página
/// no existe: cualquier request sin token redirige al SSO del hub.
/// Emite un ClaimsPrincipal con exactamente los mismos claims que el JWT del hub.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AuthMode _authMode;
    private readonly IConfiguration _config;
    private readonly UsuarioProvisioningService _provisioning;

    public LoginModel(
        AuthMode authMode,
        IConfiguration config,
        UsuarioProvisioningService provisioning)
    {
        _authMode = authMode;
        _config = config;
        _provisioning = provisioning;
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

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (!_authMode.DevLoginActivo)
        {
            // En producción el login lo hace el hub; ir a inicio dispara el SSO.
            return Redirect("~/");
        }

        ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "~/" : returnUrl;
        Input.Username = _config["Auth:DevUser:PcLoginId"] ?? string.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_authMode.DevLoginActivo)
        {
            return Redirect("~/");
        }

        ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? "~/" : ReturnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var devPassword = _config["Auth:DevPassword"] ?? "dev";

        if (!string.Equals(Input.Password, devPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Contraseña de desarrollo incorrecta.");
            return Page();
        }

        var info = ConstruirHubUserInfo(Input.Username.Trim());

        var usuario = await _provisioning.ObtenerOCrearUsuarioAsync(info);

        var identity = MeaxClaims.CrearIdentidad(
            CookieAuthenticationDefaults.AuthenticationScheme,
            usuario,
            info);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!Url.IsLocalUrl(ReturnUrl))
        {
            ReturnUrl = "~/";
        }

        return LocalRedirect(ReturnUrl!);
    }

    /// <summary>
    /// Si el usuario tecleado es el de Auth:DevUser se usa su perfil completo;
    /// con otro usuario se conserva el SystemRole de config pero sin nómina ni
    /// puesto (evita matches falsos contra Tress/Expedientes).
    /// </summary>
    private HubUserInfo ConstruirHubUserInfo(string username)
    {
        var devUser = _config.GetSection("Auth:DevUser");
        var configLogin = devUser["PcLoginId"] ?? string.Empty;

        if (string.Equals(username, configLogin, StringComparison.OrdinalIgnoreCase))
        {
            return new HubUserInfo(
                configLogin,
                devUser["DisplayName"],
                devUser["Department"],
                devUser["Position"],
                devUser["EmployeeId"],
                devUser["SystemRole"]);
        }

        return new HubUserInfo(
            username,
            username,
            devUser["Department"],
            null,
            null,
            devUser["SystemRole"]);
    }
}
