namespace qcSolicitudDesviacion.ViewModels;

public class QcDesvAprobadorRolCrearVm
{
    public string RolCodigo { get; set; } = string.Empty;

    public string NombreEs { get; set; } = string.Empty;

    public string NombreEn { get; set; } = string.Empty;

    public string Grupo { get; set; } = string.Empty;

    public int Orden { get; set; }

    public int? UsuarioSeleccionadoId { get; set; }

    public List<QcDesvAprobadorUsuarioCrearVm> Usuarios { get; set; } = new();
}

public class QcDesvAprobadorUsuarioCrearVm
{
    public int UsuarioId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Departamento { get; set; }
}

public class QcDesvAprobadorSeleccionadoFormModel
{
    public string RolCodigo { get; set; } = string.Empty;

    public int? UsuarioAprobadorId { get; set; }
}