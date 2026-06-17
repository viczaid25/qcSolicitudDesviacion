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

    public ExtensionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        QcDesvWorkflowService workflowService)
    {
        _contextFactory = contextFactory;
        _workflowService = workflowService;
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
                cancellationToken);

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

            return ServiceResult<int>.Success(extension.Id, "Extensión solicitada correctamente.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<int>.Failure($"Error al solicitar extensión: {ex.Message}");
        }
    }
}