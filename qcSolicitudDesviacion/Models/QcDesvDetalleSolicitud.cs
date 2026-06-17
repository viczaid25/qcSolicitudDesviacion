namespace qcSolicitudDesviacion.Models;

public class QcDesvDescripcion
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }

    public string ComoDebeRealizarseProceso { get; set; } = string.Empty;
    public string ControlesOperacionActual { get; set; } = string.Empty;
    public string ProcesoDespuesCambioTemporal { get; set; } = string.Empty;
    public string ControlesDuranteCambio { get; set; } = string.Empty;

    public int CreadoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public int? ModificadoPorUsuarioId { get; set; }
    public DateTime? FechaModificacion { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvUsuario? CreadoPorUsuario { get; set; }
    public QcDesvUsuario? ModificadoPorUsuario { get; set; }
}

public class QcDesvAnalisisRiesgo
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }

    public byte SeveridadActual { get; set; }
    public byte OcurrenciaActual { get; set; }
    public byte DeteccionActual { get; set; }

    public byte OcurrenciaConCambio { get; set; }
    public byte DeteccionConCambio { get; set; }

    public int RpnActual { get; set; }
    public int RpnConCambio { get; set; }

    public int CreadoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public int? ModificadoPorUsuarioId { get; set; }
    public DateTime? FechaModificacion { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvUsuario? CreadoPorUsuario { get; set; }
    public QcDesvUsuario? ModificadoPorUsuario { get; set; }
}

public class QcDesvAccion
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }

    public string DescripcionAccion { get; set; } = string.Empty;
    public string? Responsable { get; set; }
    public DateTime? FechaCompromiso { get; set; }

    public int CreadoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public int? ModificadoPorUsuarioId { get; set; }
    public DateTime? FechaModificacion { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvUsuario? CreadoPorUsuario { get; set; }
    public QcDesvUsuario? ModificadoPorUsuario { get; set; }

    public ICollection<QcDesvArchivo> Archivos { get; set; } = new List<QcDesvArchivo>();
}

public class QcDesvArchivo
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }
    public int? AccionId { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreGuardado { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long TamanoBytes { get; set; }

    public int SubidoPorUsuarioId { get; set; }
    public DateTime FechaSubida { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvAccion? Accion { get; set; }
    public QcDesvUsuario? SubidoPorUsuario { get; set; }
}

public class QcDesvControl
{
    public int Id { get; set; }
    public int SolicitudId { get; set; }

    public string NumeroLote { get; set; } = string.Empty;
    public string NumeroCase { get; set; } = string.Empty;

    public int CerradoPorUsuarioId { get; set; }
    public DateTime FechaCierre { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
    public QcDesvUsuario? CerradoPorUsuario { get; set; }
}