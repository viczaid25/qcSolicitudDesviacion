namespace qcSolicitudDesviacion.Models;

public class QcDesvEtiquetaValor
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Valor { get; set; }
}

public class QcDesvEstadisticas
{
    public int TotalSolicitudes { get; set; }
    public int TotalExtensiones { get; set; }
    public int Vigentes { get; set; }
    public int PorVencer { get; set; }
    public int Vencidas { get; set; }
    public int Cerradas { get; set; }

    // % sobre las solicitudes ya resueltas (aprobada/cerrada/extensión aprobada,
    // rechazada o cancelada) -- las que aún están en revisión no cuentan para
    // no distorsionar la tasa con solicitudes que todavía no tienen desenlace.
    public double PorcentajeAprobacion { get; set; }
    public double PorcentajeRechazo { get; set; }

    public List<QcDesvEtiquetaValor> PorEstado { get; set; } = new();
    public List<QcDesvEtiquetaValor> Por6M { get; set; } = new();
    public List<QcDesvEtiquetaValor> PorTipo { get; set; } = new();
    public List<QcDesvEtiquetaValor> PorEstadoExtension { get; set; } = new();
    public List<QcDesvEtiquetaValor> TendenciaMensual { get; set; } = new();
    public List<QcDesvEtiquetaValor> TopUnidadesNegocio { get; set; } = new();
    public List<QcDesvEtiquetaValor> TopSolicitantes { get; set; } = new();
    public List<QcDesvEtiquetaValor> TopLineas { get; set; } = new();
}
