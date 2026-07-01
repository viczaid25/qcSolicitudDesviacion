namespace qcSolicitudDesviacion.Models;

public class QcDesvSolicitudCsr
{
    public int Id { get; set; }

    public int SolicitudId { get; set; }

    public int Numero { get; set; }

    public string ValorCsr { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; }

    public QcDesvSolicitud? Solicitud { get; set; }
}