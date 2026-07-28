using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

/// <summary>
/// Datos del usuario tal como los entrega meaxHub (o la config de dev).
/// EmployeeId (nómina, 4-5 dígitos) puede venir vacío si AD no la tiene registrada.
/// </summary>
public sealed record HubUserInfo(
    string PcLoginId,
    string? DisplayName,
    string? Department,
    string? Position,
    string? EmployeeId,
    string? SystemRole);

/// <summary>
/// Aprovisiona/actualiza el usuario local (QcDesvUsuarios) a partir de los
/// claims del hub. Compartido por el login de dev y por la validación del JWT.
/// </summary>
public class UsuarioProvisioningService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public UsuarioProvisioningService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<QcDesvUsuario> ObtenerOCrearUsuarioAsync(
        HubUserInfo info,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var userName = info.PcLoginId.Trim().ToLowerInvariant();

        var departamentoMeax = await ObtenerDepartamentoMeaxAsync(context, userName, cancellationToken);

        var usuario = await context.QcDesvUsuarios
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        if (usuario is null)
        {
            usuario = new QcDesvUsuario
            {
                UserName = userName,
                NombreCompleto = string.IsNullOrWhiteSpace(info.DisplayName)
                    ? userName
                    : info.DisplayName.Trim(),
                // El JWT del hub no trae correo; queda el fallback hasta que
                // otra fuente lo complete.
                Email = $"{userName}@sin-correo.local",
                Departamento = departamentoMeax ?? info.Department,
                UnidadNegocio = null,
                Activo = true,
                FechaCreacion = DateTime.Now
            };

            context.QcDesvUsuarios.Add(usuario);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            usuario.NombreCompleto = string.IsNullOrWhiteSpace(info.DisplayName)
                ? usuario.NombreCompleto
                : info.DisplayName.Trim();

            // No se pisa el email existente: el hub no trae correo.
            usuario.Departamento = departamentoMeax ?? info.Department ?? usuario.Departamento;
            usuario.Activo = true;

            await context.SaveChangesAsync(cancellationToken);
        }

        var tieneRolSolicitante = await context.QcDesvUsuarioRoles
            .AnyAsync(
                x => x.UsuarioId == usuario.Id && x.RolCodigo == "SOLICITANTE",
                cancellationToken);

        if (!tieneRolSolicitante)
        {
            context.QcDesvUsuarioRoles.Add(new QcDesvUsuarioRol
            {
                UsuarioId = usuario.Id,
                RolCodigo = "SOLICITANTE",
                FechaAsignacion = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return usuario;
    }

    private static async Task<string?> ObtenerDepartamentoMeaxAsync(
        ApplicationDbContext context,
        string login,
        CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(cancellationToken);
    }
}
