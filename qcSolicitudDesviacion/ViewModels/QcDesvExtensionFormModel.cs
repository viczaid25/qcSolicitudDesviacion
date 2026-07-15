namespace qcSolicitudDesviacion.ViewModels;

public class QcDesvExtensionFormModel
{
    public int SolicitudId { get; set; }

    public DateTime NuevaFechaVigencia { get; set; } = DateTime.Today;

    public string RazonExtension { get; set; } = string.Empty;

    public List<QcDesvAprobadorSeleccionadoFormModel> AprobadoresSeleccionados { get; set; } = new();
}