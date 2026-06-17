namespace qcSolicitudDesviacion.Models;

public class QcDesvCatTipoDesviacion
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvSolicitud> Solicitudes { get; set; } = new List<QcDesvSolicitud>();
}

public class QcDesvCatRequisitoProceso
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvSolicitud> Solicitudes { get; set; } = new List<QcDesvSolicitud>();
}

public class QcDesvCatEstadoSolicitud
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvSolicitud> Solicitudes { get; set; } = new List<QcDesvSolicitud>();
    public ICollection<QcDesvFlujoAprobacion> FlujosAprobacion { get; set; } = new List<QcDesvFlujoAprobacion>();
}

public class QcDesvCatEstadoAprobacion
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvAprobacion> Aprobaciones { get; set; } = new List<QcDesvAprobacion>();
}

public class QcDesvCatEstadoExtension
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvExtension> Extensiones { get; set; } = new List<QcDesvExtension>();
}

public class QcDesvCatTipoFlujo
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvFlujoAprobacion> FlujosAprobacion { get; set; } = new List<QcDesvFlujoAprobacion>();
    public ICollection<QcDesvAprobacion> Aprobaciones { get; set; } = new List<QcDesvAprobacion>();
}