namespace qcSolicitudDesviacion.ViewModels;

public class QcDesvCierreFormModel
{
    public int SolicitudId { get; set; }

    public string NumeroLoteInicio { get; set; } = string.Empty;

    public string NumeroCaseInicio { get; set; } = string.Empty;

    public string NumeroLoteFinal { get; set; } = string.Empty;

    public string NumeroCaseFinal { get; set; } = string.Empty;
}
