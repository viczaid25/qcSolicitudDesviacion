using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;

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
            return ServiceResult<int>.Failure("La solicitud es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(model.RazonExtension))
        {
            return ServiceResult<int>.Failure("La razón de la extensión es obligatoria.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .FirstOrDefaultAsync(x => x.Id == model.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult<int>.Failure("La solicitud no existe.");
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult<int>.Failure("Solo el solicitante original puede pedir extensión.");
        }

        var estadosPermitidos = new[]
        {
            QcDesvCodigos.EstadoSolicitud.Aprobada,
            QcDesvCodigos.EstadoSolicitud.ExtensionAprobada
        };

        if (!estadosPermitidos.Contains(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult<int>.Failure("Solo se puede solicitar extensión de una desviación aprobada.");
        }

        if (solicitud.NumeroExtensionActual >= 2)
        {
            return ServiceResult<int>.Failure("La desviación ya alcanzó el máximo de 2 extensiones.");
        }

        var existeExtensionPendiente = await context.QcDesvExtensiones
            .AnyAsync(x =>
                x.SolicitudId == solicitud.Id &&
                x.EstadoExtensionCodigo == QcDesvCodigos.EstadoExtension.Pendiente,
                cancellationToken);

        if (existeExtensionPendiente)
        {
            return ServiceResult<int>.Failure("Ya existe una extensión pendiente para esta desviación.");
        }

        if (model.NuevaFechaVigencia.Date <= solicitud.FechaVigencia.Date)
        {
            return ServiceResult<int>.Failure("La nueva fecha de vigencia debe ser mayor a la fecha actual de vigencia.");
        }

        var fechaMaximaPermitida = solicitud.FechaCreacion.Date.AddMonths(6);

        if (model.NuevaFechaVigencia.Date > fechaMaximaPermitida)
        {
            return ServiceResult<int>.Failure(
                $"La nueva fecha de vigencia no puede exceder 6 meses desde la creación. Fecha máxima permitida: {fechaMaximaPermitida:yyyy-MM-dd}.");
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

            var primeraAprobacion = await context.QcDesvAprobaciones
                .AsNoTracking()
                .Where(x =>
                    x.SolicitudId == solicitud.Id &&
                    x.ExtensionId == extension.Id &&
                    x.EsPasoActual)
                .FirstOrDefaultAsync(cancellationToken);

            if (primeraAprobacion is not null)
            {
                await _emailService.NotificarPasoEmitidoAsync(primeraAprobacion.Id, cancellationToken);
            }

            return ServiceResult<int>.Success(extension.Id, "Extensión solicitada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<int>.Failure($"Error al solicitar extensión: {ex.Message}");
        }
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
                    $"Debe seleccionar un aprobador para el rol {rolCodigo}.");
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
                    "Uno de los aprobadores seleccionados no existe o está inactivo.");
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
                    $"El usuario seleccionado para {rolCodigo} no tiene ese rol asignado.");
            }
        }

        return ServiceResult.Success();
    }
}