using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class UsuarioActualService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    private int? _usuarioIdCache;

    public UsuarioActualService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _contextFactory = contextFactory;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public int UsuarioId => _usuarioIdCache ?? 0;

    public async Task<int> ObtenerUsuarioIdAsync(
        CancellationToken cancellationToken = default)
    {
        var usuarioId = await ObtenerUsuarioIdSeguroAsync(cancellationToken);

        if (!usuarioId.HasValue || usuarioId.Value <= 0)
        {
            throw new InvalidOperationException("No se encontró el usuario autenticado en la sesión.");
        }

        return usuarioId.Value;
    }

    public async Task<QcDesvUsuario?> ObtenerUsuarioAsync(
        CancellationToken cancellationToken = default)
    {
        var usuarioId = await ObtenerUsuarioIdSeguroAsync(cancellationToken);

        if (!usuarioId.HasValue || usuarioId.Value <= 0)
        {
            return null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvUsuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == usuarioId.Value && x.Activo, cancellationToken);
    }

    public async Task<string?> ObtenerDepartamentoMeaxUsuarioAutenticadoAsync(
        CancellationToken cancellationToken = default)
    {
        var login = await ObtenerLoginRedAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var loginNormalizado = login.Trim().ToLowerInvariant();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

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

    private async Task<string?> ObtenerLoginRedAsync(
        CancellationToken cancellationToken = default)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var loginClaim =
            user.FindFirst("pc_login_id")?.Value ??
            user.FindFirst("username")?.Value;

        if (!string.IsNullOrWhiteSpace(loginClaim))
        {
            return loginClaim;
        }

        var usuarioId = await ObtenerUsuarioIdSeguroAsync(cancellationToken);

        if (!usuarioId.HasValue || usuarioId.Value <= 0)
        {
            return null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvUsuarios
            .AsNoTracking()
            .Where(x => x.Id == usuarioId.Value)
            .Select(x => x.UserName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ObtenerUsuarioIdSeguroAsync(
        CancellationToken cancellationToken = default)
    {
        if (_usuarioIdCache.HasValue && _usuarioIdCache.Value > 0)
        {
            return _usuarioIdCache.Value;
        }

        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var usuarioIdClaim =
            user.FindFirst("qc_usuario_id")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(usuarioIdClaim, out var usuarioId) || usuarioId <= 0)
        {
            return null;
        }

        _usuarioIdCache = usuarioId;

        return usuarioId;
    }
}