using Microsoft.EntityFrameworkCore;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;

namespace qcSolicitudDesviacion.Services;

public class DeviationExpirationNotificationService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviationExpirationNotificationService> _logger;

    public DeviationExpirationNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviationExpirationNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotificarVencidasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revisar desviaciones vencidas.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // El servicio se está deteniendo.
            }
        }
    }

    private async Task NotificarVencidasAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailNotificationService>();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var hoy = DateTime.Today;

        var vencidas = await context.QcDesvSolicitudes
            .Where(x =>
                !x.NotificacionVencidaEnviada &&
                x.FechaVigencia < hoy &&
                x.EstadoSolicitudCodigo != QcDesvCodigos.EstadoSolicitud.Cerrada &&
                x.EstadoSolicitudCodigo != QcDesvCodigos.EstadoSolicitud.Cancelada &&
                x.EstadoSolicitudCodigo != QcDesvCodigos.EstadoSolicitud.Rechazada)
            .ToListAsync(cancellationToken);

        if (vencidas.Count == 0)
        {
            return;
        }

        foreach (var solicitud in vencidas)
        {
            await emailService.NotificarVencidaAsync(solicitud.Id, cancellationToken);

            solicitud.NotificacionVencidaEnviada = true;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
