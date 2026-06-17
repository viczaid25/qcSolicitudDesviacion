using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class CatalogoService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CatalogoService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<QcDesvCatTipoDesviacion>> ObtenerTiposDesviacionAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvCatTipoDesviaciones
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.NombreEs)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<QcDesvCatRequisitoProceso>> ObtenerRequisitosProcesoAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.QcDesvCatRequisitosProceso
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.NombreEs)
            .ToListAsync(cancellationToken);
    }
}