using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;
using qcSolicitudDesviacion.ViewModels;

namespace qcSolicitudDesviacion.Services;

public class AdminAprobadoresService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    // QMS ya no es un rol de aprobador autoadministrable: ese paso lo
    // atiende automáticamente cualquier usuario con rol Admin.
    private static readonly string[] RolesAprobadores =
    {
        QcDesvCodigos.Rol.CoordApqp,
        QcDesvCodigos.Rol.CoordQc,
        QcDesvCodigos.Rol.ManagerQc,
        QcDesvCodigos.Rol.ManagerProdIng,
        QcDesvCodigos.Rol.AdvisorQc,
        QcDesvCodigos.Rol.DgmQc,
        QcDesvCodigos.Rol.DgmProdIng
    };

    private static readonly string[] RolesSolicitud =
    {
        QcDesvCodigos.Rol.CoordApqp,
        QcDesvCodigos.Rol.CoordQc,
        QcDesvCodigos.Rol.ManagerQc,
        QcDesvCodigos.Rol.ManagerProdIng,
        QcDesvCodigos.Rol.AdvisorQc
    };

    public AdminAprobadoresService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<bool> UsuarioEsAdminAsync(
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.RolCodigo == QcDesvCodigos.Rol.Admin,
                cancellationToken);
    }

    public async Task<List<AdminMeaxUsuarioVm>> BuscarUsuariosMeaxAsync(
        string busqueda,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(busqueda) || busqueda.Trim().Length < 2)
        {
            return new List<AdminMeaxUsuarioVm>();
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var filtro = $"%{busqueda.Trim()}%";

        return await context.Database
            .SqlQuery<AdminMeaxUsuarioVm>($"""
                SELECT TOP (50)
                    LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id]))) AS [PcLoginId],
                    ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(200), [username]))), '') AS [Nombre],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(200), [email]))), '') AS [Email],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [department]))), '') AS [Departamento],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [position]))), '') AS [Puesto],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [dep_2]))), '') AS [Dep2]
                FROM [dbo].[meax_all_user]
                WHERE
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id]))), '') IS NOT NULL
                    AND
                    (
                        LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id]))) LIKE {filtro}
                        OR ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(200), [username]))), '') LIKE {filtro}
                        OR ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(200), [email]))), '') LIKE {filtro}
                        OR ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(150), [department]))), '') LIKE {filtro}
                        OR ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(150), [position]))), '') LIKE {filtro}
                    )
                ORDER BY
                    ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(200), [username]))), ''),
                    LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id])))
                """)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<int>> AgregarUsuarioDesdeMeaxAsync(
        string pcLoginId,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (!await UsuarioEsAdminAsync(usuarioActualId, cancellationToken))
        {
            return ServiceResult<int>.Failure("No tiene permisos para agregar usuarios.");
        }

        if (string.IsNullOrWhiteSpace(pcLoginId))
        {
            return ServiceResult<int>.Failure("Debe seleccionar un usuario válido.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var loginNormalizado = pcLoginId.Trim().ToLowerInvariant();

        var usuarioMeax = await context.Database
            .SqlQuery<AdminMeaxUsuarioVm>($"""
                SELECT TOP (1)
                    LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id]))) AS [PcLoginId],
                    ISNULL(LTRIM(RTRIM(CONVERT(nvarchar(200), [username]))), '') AS [Nombre],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(200), [email]))), '') AS [Email],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [department]))), '') AS [Departamento],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [position]))), '') AS [Puesto],
                    NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(150), [dep_2]))), '') AS [Dep2]
                FROM [dbo].[meax_all_user]
                WHERE LOWER(LTRIM(RTRIM(CONVERT(nvarchar(100), [pc_login_id])))) = {loginNormalizado}
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (usuarioMeax is null)
        {
            return ServiceResult<int>.Failure("No se encontró el usuario en meax_all_user.");
        }

        var emailNormalizado = usuarioMeax.Email?.Trim().ToLowerInvariant();

        var usuarioExistente = await context.QcDesvUsuarios
            .FirstOrDefaultAsync(x =>
                x.UserName.ToLower() == loginNormalizado ||
                (!string.IsNullOrWhiteSpace(emailNormalizado) &&
                 x.Email != null &&
                 x.Email.ToLower() == emailNormalizado),
                cancellationToken);

        if (usuarioExistente is not null)
        {
            usuarioExistente.UserName = usuarioMeax.PcLoginId.Trim();
            usuarioExistente.NombreCompleto = string.IsNullOrWhiteSpace(usuarioMeax.Nombre)
                ? usuarioMeax.PcLoginId.Trim()
                : usuarioMeax.Nombre.Trim();
            usuarioExistente.Email = usuarioMeax.Email?.Trim();
            usuarioExistente.Departamento = usuarioMeax.Departamento?.Trim();
            usuarioExistente.UnidadNegocio = usuarioMeax.Dep2?.Trim();
            usuarioExistente.Activo = true;

            await context.SaveChangesAsync(cancellationToken);

            return ServiceResult<int>.Success(
                usuarioExistente.Id,
                "El usuario ya existía en el sistema y fue actualizado.");
        }

        var nuevoUsuario = new QcDesvUsuario
        {
            UserName = usuarioMeax.PcLoginId.Trim(),
            NombreCompleto = string.IsNullOrWhiteSpace(usuarioMeax.Nombre)
                ? usuarioMeax.PcLoginId.Trim()
                : usuarioMeax.Nombre.Trim(),
            Email = usuarioMeax.Email?.Trim(),
            Departamento = usuarioMeax.Departamento?.Trim(),
            UnidadNegocio = usuarioMeax.Dep2?.Trim(),
            Activo = true
        };

        context.QcDesvUsuarios.Add(nuevoUsuario);

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Success(
            nuevoUsuario.Id,
            "Usuario agregado correctamente. Ahora puede asignarle roles de aprobación.");
    }

    public async Task<AdminAprobadoresData> ObtenerDatosAsync(
        string? busqueda = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.QcDesvUsuarios
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var texto = busqueda.Trim();

            query = query.Where(x =>
                x.UserName.Contains(texto) ||
                x.NombreCompleto.Contains(texto) ||
                (x.Email != null && x.Email.Contains(texto)) ||
                (x.Departamento != null && x.Departamento.Contains(texto)));
        }

        var usuarios = await query
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.NombreCompleto)
            .Take(200)
            .ToListAsync(cancellationToken);

        var usuariosIds = usuarios.Select(x => x.Id).ToList();

        var rolesUsuarios = usuariosIds.Count == 0
            ? new List<QcDesvUsuarioRol>()
            : await context.QcDesvUsuarioRoles
                .AsNoTracking()
                .Where(x => usuariosIds.Contains(x.UsuarioId))
                .ToListAsync(cancellationToken);

        var usuariosVm = usuarios.Select(usuario => new AdminAprobadorUsuarioVm
        {
            UsuarioId = usuario.Id,
            UserName = usuario.UserName,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Departamento = usuario.Departamento,
            UnidadNegocio = usuario.UnidadNegocio,
            Activo = usuario.Activo,
            Roles = rolesUsuarios
                .Where(r => r.UsuarioId == usuario.Id)
                .Select(r => r.RolCodigo)
                .OrderBy(r => r)
                .ToList()
        }).ToList();

        var roles = ObtenerRolesAdministrables();

        var todosUsuariosConRoles = await ObtenerUsuariosConRolesAprobadorAsync(context, cancellationToken);

        foreach (var rol in roles)
        {
            rol.Usuarios = todosUsuariosConRoles
                .Where(x => x.Roles.Contains(rol.Codigo))
                .OrderBy(x => x.NombreCompleto)
                .ToList();
        }

        return new AdminAprobadoresData
        {
            Usuarios = usuariosVm,
            Roles = roles
        };
    }

    public async Task<ServiceResult> ActualizarRolesUsuarioAsync(
        int usuarioId,
        List<string> rolesSeleccionados,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (!await UsuarioEsAdminAsync(usuarioActualId, cancellationToken))
        {
            return ServiceResult.Failure("No tiene permisos para administrar aprobadores.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var usuario = await context.QcDesvUsuarios
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return ServiceResult.Failure("El usuario seleccionado no existe.");
        }

        var rolesNormalizados = rolesSeleccionados
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var rolesInvalidos = rolesNormalizados
            .Where(x => !RolesAprobadores.Contains(x))
            .ToList();

        if (rolesInvalidos.Count > 0)
        {
            return ServiceResult.Failure($"Hay roles no válidos: {string.Join(", ", rolesInvalidos)}.");
        }

        var rolesActuales = await context.QcDesvUsuarioRoles
            .Where(x =>
                x.UsuarioId == usuarioId &&
                RolesAprobadores.Contains(x.RolCodigo))
            .ToListAsync(cancellationToken);

        var rolesAEliminar = rolesActuales
            .Where(x => !rolesNormalizados.Contains(x.RolCodigo))
            .ToList();

        context.QcDesvUsuarioRoles.RemoveRange(rolesAEliminar);

        var rolesActualesSet = rolesActuales
            .Select(x => x.RolCodigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var rolCodigo in rolesNormalizados)
        {
            if (rolesActualesSet.Contains(rolCodigo))
            {
                continue;
            }

            context.QcDesvUsuarioRoles.Add(new QcDesvUsuarioRol
            {
                UsuarioId = usuarioId,
                RolCodigo = rolCodigo,
                FechaAsignacion = DateTime.Now
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success("Roles de aprobador actualizados correctamente.");
    }

    public Task<List<QcDesvAprobadorRolCrearVm>> ObtenerAprobadoresParaSolicitudAsync(
        CancellationToken cancellationToken = default) =>
        ObtenerAprobadoresPorRolesAsync(RolesSolicitud, cancellationToken);

    public Task<List<QcDesvAprobadorRolCrearVm>> ObtenerAprobadoresParaExtensionAsync(
        CancellationToken cancellationToken = default) =>
        ObtenerAprobadoresPorRolesAsync(RolesAprobadores, cancellationToken);

    private async Task<List<QcDesvAprobadorRolCrearVm>> ObtenerAprobadoresPorRolesAsync(
        string[] rolesFiltro,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var roles = ObtenerRolesAdministrables()
            .Where(x => rolesFiltro.Contains(x.Codigo))
            .OrderBy(x => x.Orden)
            .ToList();

        var codigosRol = roles
            .Select(x => x.Codigo)
            .ToList();

        var asignaciones = await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .Where(x => codigosRol.Contains(x.RolCodigo))
            .ToListAsync(cancellationToken);

        var usuariosIds = asignaciones
            .Select(x => x.UsuarioId)
            .Distinct()
            .ToList();

        var usuarios = usuariosIds.Count == 0
            ? new List<QcDesvUsuario>()
            : await context.QcDesvUsuarios
                .AsNoTracking()
                .Where(x => usuariosIds.Contains(x.Id) && x.Activo)
                .OrderBy(x => x.NombreCompleto)
                .ToListAsync(cancellationToken);

        return roles.Select(rol => new QcDesvAprobadorRolCrearVm
        {
            RolCodigo = rol.Codigo,
            NombreEs = rol.NombreEs,
            NombreEn = rol.NombreEn,
            Grupo = rol.Grupo,
            Orden = rol.Orden,
            Usuarios = asignaciones
                .Where(a => a.RolCodigo == rol.Codigo)
                .Join(
                    usuarios,
                    asignacion => asignacion.UsuarioId,
                    usuario => usuario.Id,
                    (asignacion, usuario) => new QcDesvAprobadorUsuarioCrearVm
                    {
                        UsuarioId = usuario.Id,
                        UserName = usuario.UserName,
                        NombreCompleto = usuario.NombreCompleto,
                        Email = usuario.Email,
                        Departamento = usuario.Departamento
                    })
                .OrderBy(x => x.NombreCompleto)
                .ToList()
        }).ToList();
    }

    public async Task<ServiceResult> CambiarActivoUsuarioAsync(
        int usuarioId,
        bool activo,
        int usuarioActualId,
        CancellationToken cancellationToken = default)
    {
        if (!await UsuarioEsAdminAsync(usuarioActualId, cancellationToken))
        {
            return ServiceResult.Failure("No tiene permisos para administrar usuarios.");
        }

        if (usuarioId == usuarioActualId && !activo)
        {
            return ServiceResult.Failure("No puede desactivar su propio usuario.");
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var usuario = await context.QcDesvUsuarios
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken);

        if (usuario is null)
        {
            return ServiceResult.Failure("El usuario seleccionado no existe.");
        }

        usuario.Activo = activo;

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(activo
            ? "Usuario activado correctamente."
            : "Usuario desactivado correctamente.");
    }

    private static List<AdminAprobadorRolVm> ObtenerRolesAdministrables()
    {
        // QMS ya no aparece aquí: ese paso del flujo lo atiende
        // automáticamente cualquier usuario con rol Admin, sin necesidad de
        // asignar el rol QMS a nadie en particular.
        return new List<AdminAprobadorRolVm>
        {
            new()
            {
                Codigo = QcDesvCodigos.Rol.CoordApqp,
                NombreEs = "Coordinador APQP",
                NombreEn = "APQP Coordinator",
                Grupo = "Solicitud inicial",
                Orden = 2
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.CoordQc,
                NombreEs = "Coordinador QC",
                NombreEn = "QC Coordinator",
                Grupo = "Solicitud inicial",
                Orden = 3
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.ManagerQc,
                NombreEs = "Manager QC",
                NombreEn = "QC Manager",
                Grupo = "Solicitud inicial",
                Orden = 4
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.ManagerProdIng,
                NombreEs = "Manager Producción / Ingeniería",
                NombreEn = "Production / Engineering Manager",
                Grupo = "Solicitud inicial",
                Orden = 5
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.AdvisorQc,
                NombreEs = "Advisor QC",
                NombreEn = "QC Advisor",
                Grupo = "Solicitud inicial",
                Orden = 6
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.DgmQc,
                NombreEs = "DGM QC",
                NombreEn = "DGM QC",
                Grupo = "Extensiones",
                Orden = 7
            },
            new()
            {
                Codigo = QcDesvCodigos.Rol.DgmProdIng,
                NombreEs = "DGM Producción / Ingeniería",
                NombreEn = "DGM Production / Engineering",
                Grupo = "Extensiones",
                Orden = 8
            }
        };
    }

    private static async Task<List<AdminAprobadorUsuarioVm>> ObtenerUsuariosConRolesAprobadorAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var asignaciones = await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .Where(x => RolesAprobadores.Contains(x.RolCodigo))
            .ToListAsync(cancellationToken);

        var usuariosIds = asignaciones
            .Select(x => x.UsuarioId)
            .Distinct()
            .ToList();

        if (usuariosIds.Count == 0)
        {
            return new List<AdminAprobadorUsuarioVm>();
        }

        var usuarios = await context.QcDesvUsuarios
            .AsNoTracking()
            .Where(x => usuariosIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return usuarios.Select(usuario => new AdminAprobadorUsuarioVm
        {
            UsuarioId = usuario.Id,
            UserName = usuario.UserName,
            NombreCompleto = usuario.NombreCompleto,
            Email = usuario.Email,
            Departamento = usuario.Departamento,
            UnidadNegocio = usuario.UnidadNegocio,
            Activo = usuario.Activo,
            Roles = asignaciones
                .Where(r => r.UsuarioId == usuario.Id)
                .Select(r => r.RolCodigo)
                .OrderBy(r => r)
                .ToList()
        }).ToList();
    }
}