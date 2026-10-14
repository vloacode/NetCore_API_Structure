using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using KitApi.Application.Abstractions.Services;

namespace KitApi.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required] public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 587;
    /// <summary>true = STARTTLS (puerto 587); false = sin cifrar (solo un servidor de pruebas local, como Mailpit).</summary>
    public bool UseStartTls { get; init; } = true;
    public string? User { get; init; }
    public string? Password { get; init; }   // user-secrets / variables de entorno, nunca en appsettings
    [Required, EmailAddress] public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "KitApi";
}

/// <summary>
/// Envío por SMTP con MailKit (la librería que recomienda Microsoft en lugar de System.Net.Mail.SmtpClient).
/// Un fallo de envío se registra y NO hace fallar la operación, que ya se guardó (efectos externos después del commit).
/// Si un email no puede perderse, usar outbox (ai/suggestions-catalog.md).
/// </summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _smtp = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
            if (!string.IsNullOrEmpty(_smtp.User))
                await client.AuthenticateAsync(_smtp.User, _smtp.Password ?? string.Empty, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sin el destinatario completo en el log (dato personal): solo el dominio.
            logger.LogError(ex, "No se pudo enviar el email \"{Subject}\" a un destinatario de {Domain}", subject, to.Split('@').LastOrDefault());
        }
    }
}
