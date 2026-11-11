using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;
using qcSolicitudDesviacion.Localization;

namespace qcSolicitudDesviacion.Services;

public class CierreDesviacionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CierreDesviacionService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ServiceResult> CerrarAsync(
        QcDesvCierreFormModel model,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (model.SolicitudId <= 0)
        {
            return ServiceResult.Failure(Loc.T("The request is required."));
        }

        if (string.IsNullOrWhiteSpace(model.NumeroLoteInicio))
        {
            return ServiceResult.Failure(Loc.T("The starting lot number is required."));
        }

        if (string.IsNullOrWhiteSpace(model.NumeroCaseInicio))
        {
            return ServiceResult.Failure(Loc.T("The starting case number is required."));
        }

        if (string.IsNullOrWhiteSpace(model.NumeroLoteFinal))
        {
            return ServiceResult.Failure(Loc.T("The final lot number is required."));
        }

        if (string.IsNullOrWhiteSpace(model.NumeroCaseFinal))
        {
            return ServiceResult.Failure(Loc.T("The final case number is required."));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var solicitud = await context.QcDesvSolicitudes
            .FirstOrDefaultAsync(x => x.Id == model.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return ServiceResult.Failure(Loc.T("The request does not exist."));
        }

        if (solicitud.UsuarioSolicitanteId != usuarioActualId)
        {
            return ServiceResult.Failure(Loc.T("Only the original applicant can close the deviation."));
        }

        var estadosPermitidos = new[]
        {
            QcDesvCodigos.EstadoSolicitud.Aprobada,
            QcDesvCodigos.EstadoSolicitud.ExtensionAprobada
        };

        if (!estadosPermitidos.Contains(solicitud.EstadoSolicitudCodigo))
        {
            return ServiceResult.Failure(Loc.T("Only an approved deviation can be closed."));
        }

        var yaTieneCierre = await context.QcDesvControles
            .AnyAsync(x => x.SolicitudId == solicitud.Id, cancellationToken);

        if (yaTieneCierre)
        {
            return ServiceResult.Failure(Loc.T("This deviation already has closing data."));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var estadoAnterior = solicitud.EstadoSolicitudCodigo;

            var control = new QcDesvControl
            {
                SolicitudId = solicitud.Id,
                NumeroLoteInicio = model.NumeroLoteInicio.Trim(),
                NumeroCaseInicio = model.NumeroCaseInicio.Trim(),
                NumeroLoteFinal = model.NumeroLoteFinal.Trim(),
                NumeroCaseFinal = model.NumeroCaseFinal.Trim(),
                CerradoPorUsuarioId = usuarioActualId,
                FechaCierre = DateTime.Now
            };

            context.QcDesvControles.Add(control);

            solicitud.EstadoSolicitudCodigo = QcDesvCodigos.EstadoSolicitud.Cerrada;
            solicitud.ModificadoPorUsuarioId = usuarioActualId;
            solicitud.FechaModificacion = DateTime.Now;

            context.QcDesvHistoriales.Add(new QcDesvHistorial
            {
                SolicitudId = solicitud.Id,
                Accion = "Desviación cerrada",
                EstadoAnteriorCodigo = estadoAnterior,
                EstadoNuevoCodigo = QcDesvCodigos.EstadoSolicitud.Cerrada,
                Comentario = $"Lote inicio: {model.NumeroLoteInicio.Trim()} | Case inicio: {model.NumeroCaseInicio.Trim()} | " +
                             $"Lote final: {model.NumeroLoteFinal.Trim()} | Case final: {model.NumeroCaseFinal.Trim()}",
                UsuarioId = usuarioActualId,
                FechaMovimiento = DateTime.Now
            });

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ServiceResult.Success(Loc.T("Deviation closed successfully."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure(Loc.T("Error closing the deviation: {0}", ex.Message));
        }
    }
}
