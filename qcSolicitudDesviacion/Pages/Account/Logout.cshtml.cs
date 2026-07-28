using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using qcSolicitudDesviacion.Auth;

namespace qcSolicitudDesviacion.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly AuthMode _authMode;
    private readonly IConfiguration _config;

    public LogoutModel(AuthMode authMode, IConfiguration config)
    {
        _authMode = authMode;
        _config = config;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (_authMode.DevLoginActivo)
        {
            return RedirectToPage("/Account/Login");
        }

        // En producción la sesión pertenece al hub: se elimina la cookie
        // .MEAX.JWT de este host y se regresa a Meax One.
        Response.Cookies.Delete(".MEAX.JWT");

        var hubBaseUrl = (_config["Hub:BaseUrl"] ?? "/").TrimEnd('/');

        return Redirect(string.IsNullOrWhiteSpace(hubBaseUrl) ? "~/" : hubBaseUrl);
    }
}
