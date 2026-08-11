namespace qcSolicitudDesviacion.Services;

public class EmailSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public string FromEmail { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }

    public string? AppBaseUrl { get; set; }

    /// <summary>
    /// Cuando tiene valor, TODOS los correos se redirigen a esta dirección en vez de a los
    /// destinatarios reales (útil para probar en Development sin notificar a usuarios reales).
    /// </summary>
    public string? TestOverrideEmail { get; set; }

    /// <summary>
    /// Buzón fijo que recibe las notificaciones del paso QMS del flujo de
    /// aprobación (ya no se elige un aprobador QMS específico).
    /// </summary>
    public string QmsNotificationEmail { get; set; } = "meax_qms_communication@meax.mx";
}
