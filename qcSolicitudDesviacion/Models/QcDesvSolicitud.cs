using qcSolicitudDesviacion.Constants;

namespace qcSolicitudDesviacion.Models;

public class QcDesvSolicitud
{
    public int Id { get; set; }

    public int UsuarioSolicitanteId { get; set; }
    public string NombreSolicitante { get; set; } = string.Empty;
    public DateTime FechaSolicitud { get; set; }

    public string DepartamentoSolicitante { get; set; } = string.Empty;
    public string UnidadNegocioAfectada { get; set; } = string.Empty;
    public string CodigoLinea { get; set; } = string.Empty;
    public string Clientes { get; set; } = string.Empty;

    public string TipoDesviacionCodigo { get; set; } = QcDesvCodigos.TipoDesviacion.Estandar;
    public DateTime FechaVigencia { get; set; }

    public int? CantidadPiezas { get; set; }
    public string NumeroParteAfectado { get; set; } = string.Empty;

    public string RequisitoProcesoCodigo { get; set; } = string.Empty;
    public string? RequisitoProcesoOtro { get; set; }

    public string? CSR1 { get; set; }
    public string? CSR2 { get; set; }
    public string? CSR3 { get; set; }

    public bool InformarCliente { get; set; }
    public string RazonInformarCliente { get; set; } = string.Empty;

    public string EstadoSolicitudCodigo { get; set; } = QcDesvCodigos.EstadoSolicitud.Borrador;
    public byte NumeroExtensionActual { get; set; }

    public int CreadoPorUsuarioId { get; set; }
    public DateTime FechaCreacion { get; set; }

    public int? ModificadoPorUsuarioId { get; set; }
    public DateTime? FechaModificacion { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QcDesvUsuario? UsuarioSolicitante { get; set; }
    public QcDesvUsuario? CreadoPorUsuario { get; set; }
    public QcDesvUsuario? ModificadoPorUsuario { get; set; }

    public QcDesvCatTipoDesviacion? TipoDesviacion { get; set; }
    public QcDesvCatRequisitoProceso? RequisitoProceso { get; set; }
    public QcDesvCatEstadoSolicitud? EstadoSolicitud { get; set; }

    public QcDesvDescripcion? Descripcion { get; set; }
    public QcDesvAnalisisRiesgo? AnalisisRiesgo { get; set; }
    public QcDesvControl? Control { get; set; }

    public ICollection<QcDesvAccion> Acciones { get; set; } = new List<QcDesvAccion>();
    public ICollection<QcDesvArchivo> Archivos { get; set; } = new List<QcDesvArchivo>();
    public ICollection<QcDesvExtension> Extensiones { get; set; } = new List<QcDesvExtension>();
    public ICollection<QcDesvAprobacion> Aprobaciones { get; set; } = new List<QcDesvAprobacion>();
    public ICollection<QcDesvHistorial> Historiales { get; set; } = new List<QcDesvHistorial>();
}