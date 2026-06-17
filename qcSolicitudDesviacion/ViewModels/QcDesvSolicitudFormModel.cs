using qcSolicitudDesviacion.Constants;

namespace qcSolicitudDesviacion.ViewModels;

public class QcDesvSolicitudFormModel
{
    public int SolicitudId { get; set; }
    public int UsuarioSolicitanteId { get; set; }

    public string NombreSolicitante { get; set; } = string.Empty;
    public DateTime FechaSolicitud { get; set; } = DateTime.Today;

    public string DepartamentoSolicitante { get; set; } = string.Empty;
    public string UnidadNegocioAfectada { get; set; } = string.Empty;
    public string CodigoLinea { get; set; } = string.Empty;
    public string Clientes { get; set; } = string.Empty;

    public string TipoDesviacionCodigo { get; set; } = QcDesvCodigos.TipoDesviacion.Critica;
    public DateTime FechaVigencia { get; set; } = DateTime.Today;

    public int? CantidadPiezas { get; set; }
    public string NumeroParteAfectado { get; set; } = string.Empty;

    public string RequisitoProcesoCodigo { get; set; } = string.Empty;
    public string? RequisitoProcesoOtro { get; set; }

    public string? CSR1 { get; set; }
    public string? CSR2 { get; set; }
    public string? CSR3 { get; set; }

    public bool InformarCliente { get; set; }
    public string RazonInformarCliente { get; set; } = string.Empty;

    public string ComoDebeRealizarseProceso { get; set; } = string.Empty;
    public string ControlesOperacionActual { get; set; } = string.Empty;
    public string ProcesoDespuesCambioTemporal { get; set; } = string.Empty;
    public string ControlesDuranteCambio { get; set; } = string.Empty;

    public int? SeveridadActual { get; set; }
    public int? OcurrenciaActual { get; set; }
    public int? DeteccionActual { get; set; }

    public int? OcurrenciaConCambio { get; set; }
    public int? DeteccionConCambio { get; set; }

    public string DescripcionAccion { get; set; } = string.Empty;
    public string? ResponsableAccion { get; set; }
    public DateTime? FechaCompromisoAccion { get; set; }
}