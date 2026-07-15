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