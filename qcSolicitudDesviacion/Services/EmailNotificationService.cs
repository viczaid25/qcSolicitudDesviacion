using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using qcSolicitudDesviacion.Constants;
using qcSolicitudDesviacion.Data;
using qcSolicitudDesviacion.Models;

namespace qcSolicitudDesviacion.Services;

public class EmailNotificationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IOptions<EmailSettings> settings,
        ILogger<EmailNotificationService> logger)
    {
        _contextFactory = contextFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task NotificarPasoEmitidoAsync(
        int aprobacionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var aprobacion = await context.QcDesvAprobaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == aprobacionId, cancellationToken);

        if (aprobacion is null)
        {
            return;
        }

        var destinatarios = await ObtenerCorreosAprobadorAsync(context, aprobacion, cancellationToken);

        if (destinatarios.Count == 0)
        {
            return;
        }

        var esExtension = aprobacion.ExtensionId.HasValue;
        var id = aprobacion.SolicitudId;

        var asunto = esExtension
            ? $"Deviation Request Extension ID{id}"
            : $"Deviation Request Issued ID{id}";

        var cuerpo = esExtension
            ? $"Please be informed that a Deviation Extension ID{id} has been issued and is now available for review.\n\nPlease review the request and provide your judgment by approving or rejecting it, as applicable."
            : $"Please be informed that a Deviation Request ID{id} has been issued and is now available for review.\n\nPlease review the request and provide your judgment by approving or rejecting it, as applicable.";

        await EnviarAsync(destinatarios, asunto, ConEnlace(cuerpo, $"/aprobaciones/revisar/{aprobacion.Id}"));
    }

    public async Task NotificarRechazoAsync(
        int solicitudId,
        string? posicion,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var destinatario = await ObtenerCorreoSolicitanteAsync(context, solicitudId, cancellationToken);

        if (destinatario is null)
        {
            return;
        }

        var posicionTexto = string.IsNullOrWhiteSpace(posicion) ? "the reviewer" : posicion;

        var asunto = $"Deviation Request ID{solicitudId} \"Rejected\"";
        var cuerpo =
            $"Please be informed that the Deviation Request ID{solicitudId} has been reviewed and rejected by {posicionTexto}.\n\n" +
            "Please review the comments provided in the request. If applicable, a new request may be submitted for re-evaluation.";

        await EnviarAsync(new[] { destinatario }, asunto, ConEnlace(cuerpo, $"/solicitudes/{solicitudId}"));
    }

    public async Task NotificarAprobacionFinalAsync(
        int solicitudId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var destinatario = await ObtenerCorreoSolicitanteAsync(context, solicitudId, cancellationToken);

        if (destinatario is null)
        {
            return;
        }

        var asunto = $"Deviation Request ID{solicitudId} \"Accepted\"";
        var cuerpo = $"Please be informed that the Deviation Request ID{solicitudId} has been reviewed and Approved.";

        await EnviarAsync(new[] { destinatario }, asunto, ConEnlace(cuerpo, $"/solicitudes/{solicitudId}"));
    }

    public async Task NotificarVencidaAsync(
        int solicitudId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var destinatario = await ObtenerCorreoSolicitanteAsync(context, solicitudId, cancellationToken);

        if (destinatario is null)
        {
            return;
        }

        var asunto = $"Deviation Expired ID{solicitudId} – Action Required";
        var cuerpo =
            $"The deviation ID{solicitudId} has reached its due date and remains open.\n\n" +
            "Please either:\n" +
            "- Close the deviation if all required actions have been completed, or\n" +
            "- Request an extension if additional time is needed to complete the remaining activities.";

        await EnviarAsync(new[] { destinatario }, asunto, ConEnlace(cuerpo, $"/solicitudes/{solicitudId}"));
    }

    private string ConEnlace(string cuerpo, string ruta)
    {
        if (string.IsNullOrWhiteSpace(_settings.AppBaseUrl))
        {
            return cuerpo;
        }

        return $"{cuerpo}\n\n{_settings.AppBaseUrl.TrimEnd('/')}{ruta}";
    }

    private static async Task<string?> ObtenerCorreoSolicitanteAsync(
        ApplicationDbContext context,
        int solicitudId,
        CancellationToken cancellationToken)
    {
        var correo = await context.QcDesvSolicitudes
            .AsNoTracking()
            .Where(x => x.Id == solicitudId)
            .Select(x => x.UsuarioSolicitante!.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(correo) ? null : correo;
    }

    private async Task<List<string>> ObtenerCorreosAprobadorAsync(
        ApplicationDbContext context,
        QcDesvAprobacion aprobacion,
        CancellationToken cancellationToken)
    {
        // El paso QMS ya no se asigna a un usuario ni se resuelve por el rol
        // QMS: sus notificaciones van siempre al buzón compartido de QMS.
        if (aprobacion.RolAprobadorCodigo == QcDesvCodigos.Rol.Qms)
        {
            return string.IsNullOrWhiteSpace(_settings.QmsNotificationEmail)
                ? new List<string>()
                : new List<string> { _settings.QmsNotificationEmail };
        }

        if (aprobacion.UsuarioAprobadorId.HasValue)
        {
            var correo = await context.QcDesvUsuarios
                .AsNoTracking()
                .Where(x => x.Id == aprobacion.UsuarioAprobadorId.Value && x.Activo)
                .Select(x => x.Email)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(correo) ? new List<string>() : new List<string> { correo };
        }

        var usuariosIds = await context.QcDesvUsuarioRoles
            .AsNoTracking()
            .Where(x => x.RolCodigo == aprobacion.RolAprobadorCodigo)
            .Select(x => x.UsuarioId)
            .ToListAsync(cancellationToken);

        if (usuariosIds.Count == 0)
        {
            return new List<string>();
        }

        return await context.QcDesvUsuarios
            .AsNoTracking()
            .Where(x => usuariosIds.Contains(x.Id) && x.Activo && x.Email != string.Empty)
            .Select(x => x.Email)
            .ToListAsync(cancellationToken);
    }

    private async Task EnviarAsync(
        IReadOnlyCollection<string> destinatarios,
        string asunto,
        string cuerpo)
    {
        var correos = destinatarios
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (correos.Count == 0 || string.IsNullOrWhiteSpace(_settings.SmtpServer))
        {
            return;
        }

        var destinatariosReales = correos;

        if (!string.IsNullOrWhiteSpace(_settings.TestOverrideEmail))
        {
            cuerpo = $"[TEST MODE - originally intended for: {string.Join(", ", correos)}]\n\n{cuerpo}";
            destinatariosReales = new List<string> { _settings.TestOverrideEmail };
        }

        try
        {
            using var mensaje = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail),
                Subject = asunto,
                Body = cuerpo,
                IsBodyHtml = false
            };

            foreach (var correo in destinatariosReales)
            {
                mensaje.To.Add(correo);
            }

            using var cliente = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl
            };

            await cliente.SendMailAsync(mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudo enviar la notificación por correo '{Asunto}' a {Destinatarios}.",
                asunto,
                string.Join(", ", correos));
        }
    }
}
