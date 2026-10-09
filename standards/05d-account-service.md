# Autenticación: AccountService (cuenta propia)

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** Perfil, cambio de contraseña/email, sesiones y 2FA del usuario autenticado.  
> Índice general: `standards/00-INDEX.md`

### `AccountService` — `Infrastructure/Identity/AccountService.cs`
```csharp
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Results;
using {Project}.Application.Features.Auth;

namespace {Project}.Infrastructure.Identity;

public sealed class AccountService(
    UserManager<AppUser> userManager,
    SessionManager sessions,
    ICurrentUserService currentUser,
    AccountEmails emails,
    IOptions<AppUrlOptions> appOptions) : IAccountService
{
    private const int RecoveryCodeCount = 10;

    public async Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        return user is null ? AuthErrors.NotAuthenticated : await ToProfileAsync(user, ct);
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        user.FirstName = request.FirstName?.Trim();
        user.LastName = request.LastName?.Trim();
        if (user.PhoneNumber != request.PhoneNumber)
        {
            user.PhoneNumber = request.PhoneNumber;
            user.PhoneNumberConfirmed = false;
        }

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? await ToProfileAsync(user, ct) : Error.FromIdentity(result.Errors);
    }

    public async Task<Result<AuthTokens>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? AuthErrors.InvalidPassword
                : Error.FromIdentity(result.Errors);

        // Cierra todas las sesiones (incluidas las de otros dispositivos) y entrega una nueva al llamador.
        await sessions.RevokeAllAsync(user.Id, "Password changed", ct);
        await emails.SendSecurityNoticeAsync(user.Email!, "Tu contraseña fue cambiada.", ct);
        return await sessions.IssueTokensAsync(user, familyId: null, ct);
    }

    public async Task<Result> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return AuthErrors.InvalidPassword;

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return Result.Success();

        if (await userManager.FindByEmailAsync(newEmail) is not null)
            return AuthErrors.EmailInUse;

        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        await emails.SendChangeEmailAsync(user, newEmail, token, ct);   // se confirma en /auth/confirm-email-change
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        return Result.Success(await sessions.GetActiveSessionsAsync(userId, ct));
    }

    public async Task<Result> RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        var revoked = await sessions.RevokeFamilyAsync(userId, sessionId, "Revoked by user", ct);
        return revoked > 0 ? Result.Success() : AuthErrors.SessionNotFound;
    }

    public async Task<Result> LogoutAllAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        await sessions.RevokeAllAsync(userId, "Logout all", ct);
        return Result.Success();
    }

    public async Task<Result<TwoFactorStatusDto>> GetTwoFactorStatusAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        return new TwoFactorStatusDto(
            user.TwoFactorEnabled,
            await userManager.GetAuthenticatorKeyAsync(user) is not null,
            await userManager.CountRecoveryCodesAsync(user));
    }

    public async Task<Result<TwoFactorSetupDto>> SetupAuthenticatorAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (user.TwoFactorEnabled) return AuthErrors.TwoFactorAlreadyEnabled;

        await userManager.ResetAuthenticatorKeyAsync(user);   // clave nueva en cada setup
        var key = (await userManager.GetAuthenticatorKeyAsync(user))!;

        var issuer = UrlEncoder.Default.Encode(appOptions.Value.AppName);
        var account = UrlEncoder.Default.Encode(user.Email!);
        var uri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6";

        return new TwoFactorSetupDto(FormatKey(key), uri);   // el frontend genera el QR con "uri"
    }

    public async Task<Result<RecoveryCodesDto>> EnableTwoFactorAsync(TwoFactorCodeRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (user.TwoFactorEnabled) return AuthErrors.TwoFactorAlreadyEnabled;
        if (await userManager.GetAuthenticatorKeyAsync(user) is null) return AuthErrors.AuthenticatorNotConfigured;

        var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
            return AuthErrors.InvalidTwoFactorCode;

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await emails.SendSecurityNoticeAsync(user.Email!, "Se activó la verificación en dos pasos en tu cuenta.", ct);

        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    public async Task<Result> DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (!user.TwoFactorEnabled) return AuthErrors.TwoFactorNotEnabled;

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            return AuthErrors.InvalidPassword;

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);   // re-activar exige un setup nuevo
        await emails.SendSecurityNoticeAsync(user.Email!, "Se desactivó la verificación en dos pasos en tu cuenta.", ct);
        return Result.Success();
    }

    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (!user.TwoFactorEnabled) return AuthErrors.TwoFactorNotEnabled;

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    private async Task<AppUser?> GetCurrentUserAsync()
        => currentUser.UserId is { } id ? await userManager.FindByIdAsync(id.ToString()) : null;

    private async Task<UserProfileDto> ToProfileAsync(AppUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await sessions.GetPermissionsAsync(roles, ct);
        return new UserProfileDto(user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber,
            user.EmailConfirmed, user.TwoFactorEnabled, roles.ToList(), permissions);
    }

    /// <summary>"abcd efgh ijkl ..." para escribirla a mano en la app autenticadora.</summary>
    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return sb.ToString().TrimEnd().ToLowerInvariant();
    }
}
```
