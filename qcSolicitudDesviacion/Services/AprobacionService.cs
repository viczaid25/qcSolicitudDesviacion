using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.Localization;

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
            return ServiceResult.Failure(Loc.T("The approval does not exist."));
        }

        if (!aprobacion.EsPasoActual)
        {
            return ServiceResult.Failure(Loc.T("This approval is not the current step."));
        }

        if (aprobacion.EstadoAprobacionCodigo != QcDesvCodigos.EstadoAprobacion.Pendiente)
        {
            return ServiceResult.Failure(Loc.T("This approval has already been processed."));
        }

        if (!await UsuarioPuedeResponderAprobacionAsync(
                context,
                aprobacion,
                usuarioActualId,
                cancellationToken))
        {
            return ServiceResult.Failure(Loc.T("You do not have permission to approve this request."));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        QcDesvAprobacion? siguienteAprobacion;
        QcDesvSolicitud solicitud;

        try
        {
            solicitud = aprobacion.Solicitud!;
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            aprobacion.EstadoAprobacionCodigo = QcDesvCodigos.EstadoAprobacion.Aprobado;
            aprobacion.UsuarioAprobadorId = usuarioActualId;
            aprobacion.Comentarios = comentarios;
            aprobacion.FechaDecision = DateTime.Now;
            aprobacion.EsPasoActual = false;

            siguienteAprobacion = await context.QcDesvAprobaciones
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
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult.Failure(Loc.T("Error approving: {0}", detalle));
        }

        // La aprobación ya quedó guardada (transacción confirmada arriba): un
        // fallo al notificar por correo no debe reportarse como error ni,
        // sobre todo, intentar un rollback de una transacción que ya hizo
        // commit (eso lanzaría una segunda excepción sin capturar y tumbaría
        // el circuito completo de Blazor Server).
        if (siguienteAprobacion is not null)
        {
            await _emailService.NotificarPasoEmitidoAsync(siguienteAprobacion.Id, cancellationToken);
        }
        else if (aprobacion.TipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Solicitud)
        {
            await _emailService.NotificarAprobacionFinalAsync(solicitud.Id, cancellationToken);
        }

        return ServiceResult.Success(Loc.T("Approval recorded successfully."));
    }

    public async Task<ServiceResult> RechazarAsync(
        int aprobacionId,
        int usuarioActualId,
        string comentarios,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comentarios))
        {
            return ServiceResult.Failure(Loc.T("You must enter comments to reject."));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var aprobacion = await context.QcDesvAprobaciones
            .Include(x => x.Solicitud)
            .Include(x => x.Extension)
            .Include(x => x.RolAprobador)
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);

        if (aprobacion is null)
        {
            return ServiceResult.Failure(Loc.T("The approval does not exist."));
        }

        if (!aprobacion.EsPasoActual)
        {
            return ServiceResult.Failure(Loc.T("This approval is not the current step."));
        }

        if (aprobacion.EstadoAprobacionCodigo != QcDesvCodigos.EstadoAprobacion.Pendiente)
        {
            return ServiceResult.Failure(Loc.T("This approval has already been processed."));
        }

        if (!await UsuarioPuedeResponderAprobacionAsync(
                context,
                aprobacion,
                usuarioActualId,
                cancellationToken))
        {
            return ServiceResult.Failure(Loc.T("You do not have permission to reject this request."));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        QcDesvSolicitud solicitud;

        var esRechazoDeExtension = aprobacion.TipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Extension;

        try
        {
            solicitud = aprobacion.Solicitud!;
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            aprobacion.EstadoAprobacionCodigo = QcDesvCodigos.EstadoAprobacion.Rechazado;
            aprobacion.UsuarioAprobadorId = usuarioActualId;
            aprobacion.Comentarios = comentarios.Trim();
            aprobacion.FechaDecision = DateTime.Now;
            aprobacion.EsPasoActual = false;

            // Rechazar una EXTENSIÓN no debe tumbar el estado de la solicitud
            // original -- esa ya estaba aprobada. Se regresa a lo que era
            // antes de pedir la extensión (APROBADA, o EXTENSION_APROBADA si
            // ya había una extensión previa aprobada), en vez de marcar toda
            // la desviación como RECHAZADA.
            solicitud.EstadoSolicitudCodigo = esRechazoDeExtension
                ? (solicitud.NumeroExtensionActual > 0
                    ? QcDesvCodigos.EstadoSolicitud.ExtensionAprobada
                    : QcDesvCodigos.EstadoSolicitud.Aprobada)
                : QcDesvCodigos.EstadoSolicitud.Rechazada;

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
                Accion = esRechazoDeExtension ? "Extensión rechazada" : "Solicitud rechazada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = solicitud.EstadoSolicitudCodigo,
                Comentario = comentarios.Trim(),
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var detalle = ex.InnerException?.Message ?? ex.Message;

            return ServiceResult.Failure(Loc.T("Error rejecting: {0}", detalle));
        }

        // La solicitud ya quedó guardada (transacción confirmada arriba): un
        // fallo al notificar por correo no debe reportarse como error ni,
        // sobre todo, intentar un rollback de una transacción que ya hizo
        // commit (eso lanzaría una segunda excepción sin capturar y tumbaría
        // el circuito completo de Blazor Server).
        var posicion = aprobacion.RolAprobador?.NombreEn ?? aprobacion.RolAprobadorCodigo;
        await _emailService.NotificarRechazoAsync(solicitud.Id, posicion, esRechazoDeExtension, cancellationToken);

        return ServiceResult.Success(esRechazoDeExtension
            ? Loc.T("Extension rejected successfully.")
            : Loc.T("Request rejected successfully."));
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