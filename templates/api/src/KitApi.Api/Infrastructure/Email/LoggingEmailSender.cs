using KitApi.Application.Abstractions.Services;

namespace KitApi.Infrastructure.Email;

/// <summary>
/// Implementación de DESARROLLO: escribe el email en el log (incluye el enlace con el token).
/// En producción registrar una implementación real (SMTP con MailKit, SendGrid, etc.) con la misma interfaz.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL (dev) To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
