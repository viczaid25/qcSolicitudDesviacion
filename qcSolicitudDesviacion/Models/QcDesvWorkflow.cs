using qcSolicitudDesviacion.Constants;

namespace qcSolicitudDesviacion.Models;

public class QcDesvFlujoAprobacion
{
    public int Id { get; set; }

    public string TipoFlujoCodigo { get; set; } = string.Empty;
    public int Orden { get; set; }

    public string RolCodigo { get; set; } = string.Empty;
    public string EstadoSolicitudCodigo { get; set; } = string.Empty;

    public string NombrePasoEs { get; set; } = string.Empty;
    public string NombrePasoEn { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public QcDesvCatTipoFlujo? TipoFlujo { get; set; }
    public QcDesvRol? Rol { get; set; }
    public QcDesvCatEstadoSolicitud? EstadoSolicitud { get; set; }
}

public class QcDesvExtension
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public byte NumeroExtension { get; set; }

    public DateTime FechaVigenciaAnterior { get; set; }
    public DateTime NuevaFechaVigencia { get; set; }

    public string RazonExtension { get; set; } = string.Empty;

    public string EstadoExtensionCodigo { get; set; } = QcDesvCodigos.EstadoExtension.Pendiente;

    public int SolicitadaPorUsuarioId { get; set; }
    public DateTime FechaSolicitud { get; set; }

    public DateTime? FechaAprobacion { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvCatEstadoExtension? EstadoExtension { get; set; }
    public QcDesvUsuario? SolicitadaPorUsuario { get; set; }

    public ICollection<QcDesvAprobacion> Aprobaciones { get; set; } = new List<QcDesvAprobacion>();
    public ICollection<QcDesvHistorial> Historiales { get; set; } = new List<QcDesvHistorial>();
}

public class QcDesvAprobacion
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public int? ExtensionId { get; set; }

    public string TipoFlujoCodigo { get; set; } = QcDesvCodigos.TipoFlujo.Solicitud;
    public int Orden { get; set; }

    public string RolAprobadorCodigo { get; set; } = string.Empty;
    public int? UsuarioAprobadorId { get; set; }

    public string EstadoAprobacionCodigo { get; set; } = QcDesvCodigos.EstadoAprobacion.Pendiente;
    public string? Comentarios { get; set; }

    public bool EsPasoActual { get; set; }

    public DateTime FechaAsignacion { get; set; }
    public DateTime? FechaDecision { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvExtension? Extension { get; set; }
    public QcDesvCatTipoFlujo? TipoFlujo { get; set; }
    public QcDesvRol? RolAprobador { get; set; }
    public QcDesvUsuario? UsuarioAprobador { get; set; }
    public QcDesvCatEstadoAprobacion? EstadoAprobacion { get; set; }
}

public class QcDesvHistorial
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public int? ExtensionId { get; set; }

    public string Accion { get; set; } = string.Empty;
    public string? EstadoAnteriorCodigo { get; set; }
    public string? EstadoNuevoCodigo { get; set; }
    public string? Comentario { get; set; }

    public int UsuarioId { get; set; }
    public DateTime FechaMovimiento { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvExtension? Extension { get; set; }
    public QcDesvCatEstadoSolicitud? EstadoAnterior { get; set; }
    public QcDesvCatEstadoSolicitud? EstadoNuevo { get; set; }
    public QcDesvUsuario? Usuario { get; set; }
}