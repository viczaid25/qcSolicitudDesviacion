using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;
using qcSolicitudDesviacion.Localization;

namespace qcSolicitudDesviacion.Services;

public class ExtensionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly QcDesvWorkflowService _workflowService;
    private readonly EmailNotificationService _emailService;

    // QMS ya no requiere elegir un aprobador específico: lo atiende
    // cualquier usuario con rol Admin (ver AprobacionService).
    private static readonly string[] RolesAprobacionExtension =
    {
        QcDesvCodigos.Rol.CoordApqp,
        QcDesvCodigos.Rol.CoordQc,
        QcDesvCodigos.Rol.ManagerQc,
        QcDesvCodigos.Rol.ManagerProdIng,
        QcDesvCodigos.Rol.AdvisorQc,
        QcDesvCodigos.Rol.DgmQc,
        QcDesvCodigos.Rol.DgmProdIng
    };

    public ExtensionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        QcDesvWorkflowService workflowService,
        EmailNotificationService emailService)
    {
        _contextFactory = contextFactory;
        _workflowService = workflowService;
        _emailService = emailService;
    }

    public async Task<ServiceResult<int>> SolicitarExtensionAsync(
        QcDesvExtensionFormModel model,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (model.SolicitudId <= 0)
        {
            return ServiceResult<int>.Failure(Loc.T("The request is required."));
        }

        if (string.IsNullOrWhiteSpace(model.RazonExtension))
        {
            return ServiceResult<int>.Failure(Loc.T("The reason for the extension is required."));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .FirstOrDefaultAsync(x => x.Id == model.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<int>.Failure(Loc.T("The request does not exist."));
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<int>.Failure(Loc.T("Only the original applicant can request an extension."));
        }

        var estadosPermitidos = new[]
        {
            QcDesvCodigos.EstadoSolicitud.Aprobada,
            QcDesvCodigos.EstadoSolicitud.ExtensionAprobada
        };

        if (!estadosPermitidos.Contains(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<int>.Failure(Loc.T("An extension can only be requested for an approved deviation."));
        }

        if (solicitud.NumeroExtensionActual >= 2)
        {
            return ServiceResult<int>.Failure(Loc.T("The deviation has already reached the maximum of 2 extensions."));
        }

        var existeExtensionPendiente = await context.QcDesvExtensiones
            .AnyAsync(x =>
                x.SolicitudId == solicitud.Id &&
                x.EstadoExtensionCodigo == QcDesvCodigos.EstadoExtension.Pendiente,
                cancellationToken);

        if (existeExtensionPendiente)
        {
            return ServiceResult<int>.Failure(Loc.T("There is already a pending extension for this deviation."));
        }

        if (model.NuevaFechaVigencia.Date <= solicitud.FechaVigencia.Date)
        {
            return ServiceResult<int>.Failure(Loc.T("The new expiration date must be later than the current expiration date."));
        }

        var fechaMaximaPermitida = solicitud.FechaCreacion.Date.AddMonths(6);

        if (model.NuevaFechaVigencia.Date > fechaMaximaPermitida)
        {
            return ServiceResult<int>.Failure(
                Loc.T("The new expiration date cannot exceed 6 months from creation. Maximum allowed date: {0}.", fechaMaximaPermitida.ToString("yyyy-MM-dd")));
        }

        var aprobadoresSeleccionadosPorRol = ObtenerAprobadoresSeleccionadosPorRol(model);

        var validacionAprobadores = await ValidarAprobadoresSeleccionadosAsync(
            context,
            aprobadoresSeleccionadosPorRol,
            cancellationToken);

        if (!validacionAprobadores.Succeeded)
        {
            return ServiceResult<int>.Failure(validacionAprobadores.Message);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        int extensionCreadaId;

        try
        {
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;
            var numeroExtension = (byte)(solicitud.NumeroExtensionActual + 1);

            var extension = new QcDesvExtension
            {
                SolicitudId = solicitud.Id,
                NumeroExtension = numeroExtension,
                FechaVigenciaAnterior = solicitud.FechaVigencia.Date,
                NuevaFechaVigencia = model.NuevaFechaVigencia.Date,
                RazonExtension = model.RazonExtension.Trim(),
                EstadoExtensionCodigo = QcDesvCodigos.EstadoExtension.Pendiente,
                SolicitadaPorUsuarioId = usuarioActualId,
                FechaSolicitud = DateTime.Now
            };

            context.QcDesvExtensiones.Add(extension);
            await context.SaveChangesAsync(cancellationToken);

            var workflowResult = await _workflowService.CrearAprobacionesAsync(
                context,
                solicitud.Id,
                extension.Id,
                QcDesvCodigos.TipoFlujo.Extension,
                aprobadoresSeleccionadosPorRol: aprobadoresSeleccionadosPorRol,
                cancellationToken: cancellationToken);

            if (!workflowResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<int>.Failure(workflowResult.Message);
            }

            solicitud.EstadoSolicitudCodigo = workflowResult.Data!;
            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                ExtensionId = extension.Id,
                Accion = "Extensión solicitada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = solicitud.EstadoSolicitudCodigo,
                Comentario = model.RazonExtension.Trim(),
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            extensionCreadaId = extension.Id;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<int>.Failure(Loc.T("Error requesting extension: {0}", ex.Message));
        }

        // La extensión ya quedó guardada (transacción confirmada arriba): un
        // fallo al notificar por correo no debe reportarse como error ni,
        // sobre todo, intentar un rollback de una transacción que ya hizo
        // commit (eso lanzaría una segunda excepción sin capturar y tumbaría
        // el circuito completo de Blazor Server).
        var primeraAprobacion = await context.QcDesvAprobaciones
            .AsNoTracking()
            .Where(x =>
                x.SolicitudId == solicitud.Id &&
                x.ExtensionId == extensionCreadaId &&
                x.EsPasoActual)
            .FirstOrDefaultAsync(cancellationToken);

        if (primeraAprobacion is not null)
        {
            await _emailService.NotificarPasoEmitidoAsync(primeraAprobacion.Id, cancellationToken);
        }

        return ServiceResult<int>.Success(extensionCreadaId, Loc.T("Extension requested successfully."));
    }

    private static Dictionary<string, int?> ObtenerAprobadoresSeleccionadosPorRol(
        QcDesvExtensionFormModel model)
    {
        return model.AprobadoresSeleccionados
            .Where(x => !string.IsNullOrWhiteSpace(x.RolCodigo))
            .GroupBy(x => x.RolCodigo.Trim().ToUpperInvariant())
            .ToDictionary(
                x => x.Key,
                x => x.First().UsuarioAprobadorId,
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ServiceResult> ValidarAprobadoresSeleccionadosAsync(
        ApplicationDbContext context,
        Dictionary<string, int?> aprobadoresSeleccionadosPorRol,
        CancellationToken cancellationToken)
    {
        foreach (var rolCodigo in RolesAprobacionExtension)
        {
            if (!aprobadoresSeleccionadosPorRol.TryGetValue(rolCodigo, out var usuarioId) ||
                !usuarioId.HasValue ||
                usuarioId.Value <= 0)
            {
                return ServiceResult.Failure(
                    Loc.T("You must select an approver for role {0}.", rolCodigo));
            }
        }

        var usuariosIds = aprobadoresSeleccionadosPorRol
            .Values
            .Where(x => x.HasValue && x.Value > 0)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var usuariosActivos = await context.QcDesvUsuarios
            .AsNoTracking()
            .Where(x => usuariosIds.Contains(x.Id) && x.Activo)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var usuarioId in usuariosIds)
        {
            if (!usuariosActivos.Contains(usuarioId))
            {
                return ServiceResult.Failure(
                    Loc.T("One of the selected approvers does not exist or is inactive."));
            }
        }

        var asignaciones = await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .Where(x =>
                usuariosIds.Contains(x.UsuarioId) &&
                RolesAprobacionExtension.Contains(x.RolCodigo))
            .ToListAsync(cancellationToken);

        foreach (var rolCodigo in RolesAprobacionExtension)
        {
            var usuarioId = aprobadoresSeleccionadosPorRol[rolCodigo]!.Value;

            var usuarioTieneRol = asignaciones.Any(x =>
                x.UsuarioId == usuarioId &&
                x.RolCodigo == rolCodigo);

            if (!usuarioTieneRol)
            {
                return ServiceResult.Failure(
                    Loc.T("The user selected for {0} does not have that role assigned.", rolCodigo));
            }
        }

        return ServiceResult.Success();
    }
}