namespace qcSolicitudDesviacion.Constants;

/// <summary>
/// Clasifica un código de estado (solicitud, aprobación o extensión) a la
/// variante de color del sistema de badges de MEAX One (mo-badge-*),
/// siguiendo la misma semántica que sus botones: éxito = aprobado/cerrado,
/// peligro = rechazado/cancelado, primario = en proceso, secundario = borrador.
/// </summary>
public static class QcDesvEstadoBadge
{
    public static string ClaseSolicitud(string estadoCodigo) => estadoCodigo switch
    {
        QcDesvCodigos.EstadoSolicitud.Aprobada => "mo-badge-success",
        QcDesvCodigos.EstadoSolicitud.ExtensionAprobada => "mo-badge-success",
        QcDesvCodigos.EstadoSolicitud.Cerrada => "mo-badge-success",
        QcDesvCodigos.EstadoSolicitud.Rechazada => "mo-badge-danger",
        QcDesvCodigos.EstadoSolicitud.Cancelada => "mo-badge-danger",
        QcDesvCodigos.EstadoSolicitud.Borrador => "mo-badge-secondary",
        _ => "mo-badge-primary"
    };

    public static string ClaseAprobacion(string estadoCodigo) => estadoCodigo switch
    {
        QcDesvCodigos.EstadoAprobacion.Aprobado => "mo-badge-success",
        QcDesvCodigos.EstadoAprobacion.Rechazado => "mo-badge-danger",
        QcDesvCodigos.EstadoAprobacion.Cancelado => "mo-badge-secondary",
        QcDesvCodigos.EstadoAprobacion.Omitido => "mo-badge-secondary",
        _ => "mo-badge-warning"
    };

    public static string ClaseExtension(string estadoCodigo) => estadoCodigo switch
    {
        QcDesvCodigos.EstadoExtension.Aprobada => "mo-badge-success",
        QcDesvCodigos.EstadoExtension.Rechazada => "mo-badge-danger",
        QcDesvCodigos.EstadoExtension.Cancelada => "mo-badge-secondary",
        _ => "mo-badge-warning"
    };
}
