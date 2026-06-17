namespace qcSolicitudDesviacion.Models;

public class QcDesvRol
{
    public string Codigo { get; set; } = string.Empty;
    public string NombreEs { get; set; } = string.Empty;
    public string NombreEn { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<QcDesvUsuarioRol> UsuariosRoles { get; set; } = new List<QcDesvUsuarioRol>();
    public ICollection<QcDesvFlujoAprobacion> FlujosAprobacion { get; set; } = new List<QcDesvFlujoAprobacion>();
    public ICollection<QcDesvAprobacion> Aprobaciones { get; set; } = new List<QcDesvAprobacion>();
}

public class QcDesvUsuario
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string? Departamento { get; set; }
    public string? UnidadNegocio { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }

    public ICollection<QcDesvUsuarioRol> UsuariosRoles { get; set; } = new List<QcDesvUsuarioRol>();

    public ICollection<QcDesvSolicitud> SolicitudesComoSolicitante { get; set; } = new List<QcDesvSolicitud>();
    public ICollection<QcDesvSolicitud> SolicitudesCreadas { get; set; } = new List<QcDesvSolicitud>();
}

public class QcDesvUsuarioRol
{
    public int UsuarioId { get; set; }
    public string RolCodigo { get; set; } = string.Empty;
    public DateTime FechaAsignacion { get; set; }

    public QcDesvUsuario? Usuario { get; set; }
    public QcDesvRol? Rol { get; set; }
}