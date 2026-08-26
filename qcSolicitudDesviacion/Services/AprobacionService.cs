using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class AprobacionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly EmailNotificationService _emailService;

    public AprobacionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        EmailNotificationService emailService)
    {
        _contextFactory = contextFactory;
        _emailService = emailService;
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

        // El paso QMS ya no se asigna a una persona ni se resuelve por el rol
        // QMS: lo atiende cualquier Admin del sistema.
        if (rolesUsuario.Contains(QcDesvCodigos.Rol.Admin) &&
            !rolesUsuario.Contains(QcDesvCodigos.Rol.Qms))
        {
            rolesUsuario.Add(QcDesvCodigos.Rol.Qms);
        }

        // Se muestra a todo el que tenga el rol del paso, no solo al aprobador
        // específicamente asignado: así, si esa persona no puede atenderlo,
        // cualquier otro usuario del mismo nivel lo ve en su lista y lo puede
        // resolver (mismo criterio que ya se usaba para QMS).
        return await context.QcDesvAprobaciones
            .AsNoTracking()
            .Include(x => x.Solicitud)
            .Include(x => x.Extension)
            .Include(x => x.RolAprobador)
            .Include(x => x.UsuarioAprobador)
            .Where(x =>
                x.EsPasoActual &&
                x.EstadoAprobacionCodigo == QcDesvCodigos.EstadoAprobacion.Pendiente &&
                (
                    x.UsuarioAprobadorId == usuarioId ||
                    rolesUsuario.Contains(x.RolAprobadorCodigo)
                ))
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

        if (!await UsuarioPuedeResponderAprobacionAsync(
                context,
                aprobacion,
                usuarioActualId,
                cancellationToken))
        {
            return ServiceResult.Failure("No tiene permisos para aprobar esta solicitud.");
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
                    solicitud.NotificacionVencidaEnviada = false;
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

            if (siguienteAprobacion is not null)
            {
                await _emailService.NotificarPasoEmitidoAsync(siguienteAprobacion.Id, cancellationToken);
            }
            else if (aprobacion.TipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Solicitud)
            {
                await _emailService.NotificarAprobacionFinalAsync(solicitud.Id, cancellationToken);
            }

            return ServiceResult.Success("Aprobación registrada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult.Failure($"Error al aprobar: {detalle}");
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
            .Include(x => x.RolAprobador)
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

        if (!await UsuarioPuedeResponderAprobacionAsync(
                context,
                aprobacion,
                usuarioActualId,
                cancellationToken))
        {
            return ServiceResult.Failure("No tiene permisos para rechazar esta solicitud.");
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

            var posicion = aprobacion.RolAprobador?.NombreEn ?? aprobacion.RolAprobadorCodigo;
            await _emailService.NotificarRechazoAsync(solicitud.Id, posicion, cancellationToken);

            return ServiceResult.Success("Solicitud rechazada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult.Failure($"Error al rechazar: {detalle}");
        }
    }

    public async Task<QcDesvAprobacion?> ObtenerDetalleAprobacionAsync(
        int aprobacionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var aprobacion = await context.QcDesvAprobaciones
            .AsNoTracking()
            .Include(x => x.RolAprobador)
            .Include(x => x.EstadoAprobacion)
            .Include(x => x.UsuarioAprobador)
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
                    .ThenInclude(x => x.Archivos)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Archivos)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Extensiones)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Control)
            .Include(x => x.Solicitud)
                .ThenInclude(x => x!.Historiales)
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);

        // Las aprobaciones de la solicitud no se pueden Include() desde este
        // mismo root (Aprobacion -> Solicitud -> Aprobaciones) porque EF Core
        // detecta un ciclo en queries sin tracking. Se cargan aparte.
        if (aprobacion?.Solicitud is not null)
        {
            aprobacion.Solicitud.Aprobaciones = await context.QcDesvAprobaciones
                .AsNoTracking()
                .Where(x => x.SolicitudId == aprobacion.SolicitudId)
                .Include(x => x.RolAprobador)
                .Include(x => x.EstadoAprobacion)
                .Include(x => x.UsuarioAprobador)
                .ToListAsync(cancellationToken);
        }

        return aprobacion;
    }

    private static async Task<bool> UsuarioPuedeResponderAprobacionAsync(
        ApplicationDbContext context,
        QcDesvAprobacion aprobacion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        if (aprobacion.UsuarioAprobadorId == usuarioId)
        {
            return true;
        }

        // Cualquier usuario con el rol del paso puede resolverlo, no solo el
        // aprobador específicamente asignado: si esa persona no puede
        // atenderlo, otro usuario del mismo nivel puede aprobar o rechazar en
        // su lugar (mismo criterio que ya se usaba para QMS, extendido a
        // todos los roles).
        var rolParaVerificar = aprobacion.RolAprobadorCodigo == QcDesvCodigos.Rol.Qms
            ? QcDesvCodigos.Rol.Admin
            : aprobacion.RolAprobadorCodigo;

        return await UsuarioTieneRolAsync(
            context,
            usuarioId,
            rolParaVerificar,
            cancellationToken);
    }

    private static async Task<bool> UsuarioTieneRolAsync(
        ApplicationDbContext context,
        int usuarioId,
        string rolCodigo,
        CancellationToken cancellationToken)
    {
        return await context.QcDesvUsuarioRoles
            .AnyAsync(x =>
                    x.UsuarioId == usuarioId &&
                    x.RolCodigo == rolCodigo,
                cancellationToken);
    }
}