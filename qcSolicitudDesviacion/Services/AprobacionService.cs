using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class AprobacionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public AprobacionService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<QcDesvAprobacion>> ObtenerPendientesPorUsuarioAsync(
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var rolesUsuario = await context.QcDesvUsuarioRoles
            .Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.RolCodigo)
            .ToListAsync(cancellationToken);

        if (rolesUsuario.Count == 0)
        {
            return new List<QcDesvAprobacion>();
        }

        return await context.QcDesvAprobaciones
            .AsNoTracking()
            .Include(x => x.Solicitud)
            .Include(x => x.Extension)
            .Include(x => x.RolAprobador)
            .Where(x =>
                x.EsPasoActual &&
                x.EstadoAprobacionCodigo == QcDesvCodigos.EstadoAprobacion.Pendiente &&
                rolesUsuario.Contains(x.RolAprobadorCodigo))
            .OrderBy(x => x.FechaAsignacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult> AprobarAsync(
        int aprobacionId,
        int usuarioActualId,
        string? comentarios,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var aprobacion = await context.QcDesvAprobaciones
            .Include(x => x.Solicitud)
            .Include(x => x.Extension)
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);

        if (aprobacion is null)
        {
            return ServiceResult.Failure("La aprobación no existe.");
        }

        if (!aprobacion.EsPasoActual)
        {
            return ServiceResult.Failure("Esta aprobación no es el paso actual.");
        }

        if (aprobacion.EstadoAprobacionCodigo != QcDesvCodigos.EstadoAprobacion.Pendiente)
        {
            return ServiceResult.Failure("Esta aprobación ya fue procesada.");
        }

        var tieneRol = await UsuarioTieneRolAsync(
            context,
            usuarioActualId,
            aprobacion.RolAprobadorCodigo,
            cancellationToken);

        if (!tieneRol)
        {
            return ServiceResult.Failure("El usuario no tiene el rol requerido para aprobar este paso.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var solicitud = aprobacion.Solicitud!;

            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            aprobacion.EstadoAprobacionCodigo = QcDesvCodigos.EstadoAprobacion.Aprobado;
            aprobacion.UsuarioAprobadorId = usuarioActualId;
            aprobacion.Comentarios = comentarios;
            aprobacion.FechaDecision = DateTime.Now;
            aprobacion.EsPasoActual = false;

            var siguienteAprobacion = await context.QcDesvAprobaciones
                .Where(x =>
                    x.SolicitudId == aprobacion.SolicitudId &&
                    x.ExtensionId == aprobacion.ExtensionId &&
                    x.Orden > aprobacion.Orden &&
                    x.EstadoAprobacionCodigo == QcDesvCodigos.EstadoAprobacion.Pendiente)
                .OrderBy(x => x.Orden)
                .FirstOrDefaultAsync(cancellationToken);

            if (siguienteAprobacion is not null)
            {
                siguienteAprobacion.EsPasoActual = true;

                var siguienteFlujo = await context.QcDesvFlujosAprobacion
                    .FirstAsync(x =>
                        x.TipoFlujoCodigo == siguienteAprobacion.TipoFlujoCodigo &&
                        x.Orden == siguienteAprobacion.Orden,
                        cancellationToken);

                solicitud.EstadoSolicitudCodigo = siguienteFlujo.EstadoSolicitudCodigo;
            }
            else
            {
                if (aprobacion.TipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Solicitud)
                {
                    solicitud.EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Aprobada;
                }
                else
                {
                    var extension = aprobacion.Extension!;

                    extension.EstadoExtensionCodigo = QcDesvCodigos.EstadoExtension.Aprobada;
                    extension.FechaAprobacion = DateTime.Now;

                    solicitud.FechaVigencia = extension.NuevaFechaVigencia.Date;
                    solicitud.NumeroExtensionActual = extension.NumeroExtension;
                    solicitud.EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.ExtensionAprobada;
                }
            }

            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                ExtensionId = aprobacion.ExtensionId,
                Accion = "Aprobación realizada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = solicitud.EstadoSolicitudCodigo,
                Comentario = comentarios,
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult.Success("Aprobación registrada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure($"Error al aprobar: {ex.Message}");
        }
    }

    public async Task<ServiceResult> RechazarAsync(
        int aprobacionId,
        int usuarioActualId,
        string comentarios,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comentarios))
        {
            return ServiceResult.Failure("Debe capturar comentarios para rechazar.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var aprobacion = await context.QcDesvAprobaciones
            .Include(x => x.Solicitud)
            .Include(x => x.Extension)
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);

        if (aprobacion is null)
        {
            return ServiceResult.Failure("La aprobación no existe.");
        }

        if (!aprobacion.EsPasoActual)
        {
            return ServiceResult.Failure("Esta aprobación no es el paso actual.");
        }

        if (aprobacion.EstadoAprobacionCodigo != QcDesvCodigos.EstadoAprobacion.Pendiente)
        {
            return ServiceResult.Failure("Esta aprobación ya fue procesada.");
        }

        var tieneRol = await UsuarioTieneRolAsync(
            context,
            usuarioActualId,
            aprobacion.RolAprobadorCodigo,
            cancellationToken);

        if (!tieneRol)
        {
            return ServiceResult.Failure("El usuario no tiene el rol requerido para rechazar este paso.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var solicitud = aprobacion.Solicitud!;
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            aprobacion.EstadoAprobacionCodigo = QcDesvCodigos.EstadoAprobacion.Rechazado;
            aprobacion.UsuarioAprobadorId = usuarioActualId;
            aprobacion.Comentarios = comentarios.Trim();
            aprobacion.FechaDecision = DateTime.Now;
            aprobacion.EsPasoActual = false;

            solicitud.EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Rechazada;
            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            if (aprobacion.Extension is not null)
            {
                aprobacion.Extension.EstadoExtensionCodigo = QcDesvCodigos.EstadoExtension.Rechazada;
            }

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                ExtensionId = aprobacion.ExtensionId,
                Accion = "Solicitud rechazada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = QcDesvCodigos.EstadoSolicitud.Rechazada,
                Comentario = comentarios.Trim(),
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult.Success("Solicitud rechazada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure($"Error al rechazar: {ex.Message}");
        }
    }

    public async Task<QcDesvAprobacion?> ObtenerDetalleAprobacionAsync(
    int aprobacionId,
    CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvAprobaciones
            .AsNoTracking()
            .Include(x => x.RolAprobador)
            .Include(x => x.EstadoAprobacion)
            .Include(x => x.Extension)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.TipoDesviacion)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.RequisitoProceso)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.EstadoSolicitud)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Descripcion)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.AnalisisRiesgo)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Acciones)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Archivos)
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);
    }

    private static async Task<bool> UsuarioTieneRolAsync(
        ApplicationDbContext context,
        int usuarioId,
        string rolCodigo,
        CancellationToken cancellationToken)
    {
        return await context.QcDesvUsuarioRoles
            .AnyAsync(x => x.UsuarioId == usuarioId && x.RolCodigo == rolCodigo, cancellationToken);
    }
}