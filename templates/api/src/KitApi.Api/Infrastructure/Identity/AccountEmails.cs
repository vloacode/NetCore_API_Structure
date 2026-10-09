using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using KitApi.Application.Abstractions.Services;

namespace KitApi.Infrastructure.Identity;

/// <summary>
/// Construye y envía los emails de cuenta. Los tokens de Identity se codifican Base64Url para viajar en URLs.
/// Los enlaces apuntan al FRONTEND (App:ClientUrl), que luego llama al endpoint de la API correspondiente.
/// </summary>
public sealed class AccountEmails(IEmailSender sender, IOptions<AppUrlOptions> options)
{
    private readonly AppUrlOptions _app = options.Value;

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded)); }
        catch (FormatException) { return null; }
    }

    public Task SendConfirmationAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Confirma tu cuenta",
            $"Confirma tu cuenta: {Link("confirm-email", ("userId", user.Id.ToString()), ("token", EncodeToken(token)))}", ct);

    public Task SendPasswordResetAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Restablecer contraseña",
            $"Restablece tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendSetPasswordAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Tu cuenta fue creada",
            $"Un administrador creó tu cuenta. Define tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendChangeEmailAsync(AppUser user, string newEmail, string token, CancellationToken ct)
        => Send(newEmail, "Confirma tu nuevo email",
            $"Confirma el cambio de email: {Link("confirm-email-change", ("userId", user.Id.ToString()), ("newEmail", newEmail), ("token", EncodeToken(token)))}", ct);

    public Task SendAccountExistsAsync(string email, CancellationToken ct)
        => Send(email, "Intento de registro",
            $"Alguien intentó registrarse con tu email, pero ya tienes cuenta. Si olvidaste tu contraseña: {Link("forgot-password")}", ct);

    public Task SendSecurityNoticeAsync(string email, string message, CancellationToken ct)
        => Send(email, "Aviso de seguridad", message, ct);

    private string Link(string path, params (string Key, string Value)[] query)
    {
        var url = $"{_app.ClientUrl.TrimEnd('/')}/{path}";
        return query.Length == 0 ? url : QueryHelpers.AddQueryString(url, query.ToDictionary(q => q.Key, q => (string?)q.Value));
    }

    private Task Send(string to, string subject, string body, CancellationToken ct)
        => sender.SendAsync(to, subject, WebUtility.HtmlEncode(body), ct);
}
