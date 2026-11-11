namespace qcSolicitudDesviacion.Models;

public class QcDesvSolicitudResumen
{
    public int SolicitudId { get; set; }

    public int UsuarioSolicitanteId { get; set; }

    public string NombreSolicitante { get; set; } = string.Empty;
    public DateTime FechaSolicitud { get; set; }

    public string DepartamentoSolicitante { get; set; } = string.Empty;
    public string UnidadNegocioAfectada { get; set; } = string.Empty;
    public string CodigoLinea { get; set; } = string.Empty;
    public string Clientes { get; set; } = string.Empty;

    public string NumeroParteAfectado { get; set; } = string.Empty;
    public int? CantidadPiezas { get; set; }

    public DateTime FechaVigencia { get; set; }
    public byte NumeroExtensionActual { get; set; }

    public string TipoDesviacionCodigo { get; set; } = string.Empty;
    public string TipoDesviacionEs { get; set; } = string.Empty;
    public string TipoDesviacionEn { get; set; } = string.Empty;

    public string RequisitoProcesoCodigo { get; set; } = string.Empty;
    public string RequisitoProcesoEs { get; set; } = string.Empty;
    public string RequisitoProcesoEn { get; set; } = string.Empty;

    public string EstadoSolicitudCodigo { get; set; } = string.Empty;
    public string EstadoSolicitudEs { get; set; } = string.Empty;
    public string EstadoSolicitudEn { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }
}

// DTO ligero para mostrar, junto al resumen de la solicitud, el estatus
// de cada extensión que se le haya pedido -- así la lista puede distinguir
// "solicitud aprobada, extensión rechazada" en vez de mostrar un solo estado.
public class QcDesvExtensionResumen
{
    public int Id { get; set; }
    public byte NumeroExtension { get; set; }
    public string EstadoExtensionCodigo { get; set; } = string.Empty;
    public string EstadoExtensionEs { get; set; } = string.Empty;
    public string EstadoExtensionEn { get; set; } = string.Empty;
    public DateTime NuevaFechaVigencia { get; set; }
}

public class QcDesvSolicitudConExtensiones
{
    public QcDesvSolicitudResumen Solicitud { get; set; } = null!;
    public List<QcDesvExtensionResumen> Extensiones { get; set; } = new();

    public QcDesvExtensionResumen? UltimaExtension =>
        Extensiones.OrderByDescending(x => x.NumeroExtension).FirstOrDefault();
}