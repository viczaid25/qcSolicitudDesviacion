using System.Security.Claims;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.Services;

namespace qcSolicitudDesviacion.Auth;

/// <summary>
/// Nombres de claims del contrato con meaxHub (Meax One) y claims propios de la app.
/// El resto de la aplicación debe consumir únicamente estos claims — nunca AD ni la BD del hub.
/// </summary>
public static class MeaxClaims
{
    // Claims que trae el JWT del hub
    public const string Sub = "sub";
    public const string PcLoginId = "PcLoginId";
    public const string DisplayName = "DisplayName";
    public const string Department = "Department";
    public const string Position = "Position";
    public const string EmployeeId = "EmployeeId";
    public const string SystemRole = "SystemRole";
    // Idioma elegido en MEAX One ("es", "en" o "ja"); lo lee MeaxCultureProvider
    public const string Idioma = "Idioma";

    // Claims propios de la app (los consume UsuarioActualService)
    public const string QcUsuarioId = "qc_usuario_id";
    public const string PcLoginIdApp = "pc_login_id";
    public const string UserName = "username";

    /// <summary>
    /// Construye la identidad con el mismo juego de claims sin importar el modo
    /// (hub o dev), para que el resto de la app no tenga ni un solo if de modo.
    /// </summary>
    public static ClaimsIdentity CrearIdentidad(
        string authenticationScheme,
        QcDesvUsuario usuario,
        HubUserInfo info)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(QcUsuarioId, usuario.Id.ToString()),
            new(UserName, usuario.UserName),
            new(PcLoginIdApp, info.PcLoginId),
            new(PcLoginId, info.PcLoginId)
        };

        AgregarSiTieneValor(claims, DisplayName, info.DisplayName);
        AgregarSiTieneValor(claims, Department, info.Department);
        AgregarSiTieneValor(claims, Position, info.Position);
        // EmployeeId puede venir vacío si AD no tiene la nómina registrada
        AgregarSiTieneValor(claims, EmployeeId, info.EmployeeId);
        AgregarSiTieneValor(claims, SystemRole, info.SystemRole);

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, usuario.Email));
        }

        return new ClaimsIdentity(
            claims,
            authenticationScheme,
            nameType: DisplayName,
            roleType: SystemRole);
    }

    private static void AgregarSiTieneValor(List<Claim> claims, string tipo, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            claims.Add(new Claim(tipo, valor.Trim()));
        }
    }
}
