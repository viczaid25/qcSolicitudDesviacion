using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class QcDesvWorkflowService
{
    public async Task<ServiceResult<string>> CrearAprobacionesAsync(
        ApplicationDbContext context,
        int solicitudId,
        int? extensionId,
        string tipoFlujoCodigo,
        CancellationToken cancellationToken = default)
    {
        if (tipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Solicitud && extensionId.HasValue)
        {
            return ServiceResult<string>.Failure("Una solicitud inicial no debe tener ExtensionId.");
        }

        if (tipoFlujoCodigo == QcDesvCodigos.TipoFlujo.Extension && !extensionId.HasValue)
        {
            return ServiceResult<string>.Failure("El flujo de extensión requiere ExtensionId.");
        }

        var flujos = await context.QcDesvFlujosAprobacion
            .Where(x => x.TipoFlujoCodigo == tipoFlujoCodigo && x.Activo)
            .OrderBy(x => x.Orden)
            .ToListAsync(cancellationToken);

        if (flujos.Count == 0)
        {
            return ServiceResult<string>.Failure($"No existe flujo configurado para {tipoFlujoCodigo}.");
        }

        var primerOrden = flujos.First().Orden;

        foreach (var flujo in flujos)
        {
            var aprobacion = new QcDesvAprobacion
            {
                SolicitudId = solicitudId,
                ExtensionId = extensionId,
                TipoFlujoCodigo = tipoFlujoCodigo,
                Orden = flujo.Orden,
                RolAprobadorCodigo = flujo.RolCodigo,
                EstadoAprobacionCodigo = QcDesvCodigos.EstadoAprobacion.Pendiente,
                EsPasoActual = flujo.Orden == primerOrden,
                FechaAsignacion = DateTime.Now
            };

            context.QcDesvAprobaciones.Add(aprobacion);
        }

        var primerEstado = flujos.First().EstadoSolicitudCodigo;

        return ServiceResult<string>.Success(primerEstado);
    }
}