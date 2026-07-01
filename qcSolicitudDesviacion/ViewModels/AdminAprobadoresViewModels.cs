namespace qcSolicitudDesviacion.ViewModels;

public class AdminAprobadoresData
{
    public List<AdminAprobadorUsuarioVm> Usuarios { get; set; } = new();

    public List<AdminAprobadorRolVm> Roles { get; set; } = new();
}

public class AdminAprobadorUsuarioVm
{
    public int UsuarioId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NombreCompleto { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Departamento { get; set; }

    public string? UnidadNegocio { get; set; }

    public bool Activo { get; set; }

    public List<string> Roles { get; set; } = new();
}

public class AdminAprobadorRolVm
{
    public string Codigo { get; set; } = string.Empty;

    public string NombreEs { get; set; } = string.Empty;

    public string NombreEn { get; set; } = string.Empty;

    public string Grupo { get; set; } = string.Empty;

    public int Orden { get; set; }

    public List<AdminAprobadorUsuarioVm> Usuarios { get; set; } = new();
}

public class AdminMeaxUsuarioVm
{
    public string PcLoginId { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Departamento { get; set; }

    public string? Puesto { get; set; }

    public string? Dep2 { get; set; }
}