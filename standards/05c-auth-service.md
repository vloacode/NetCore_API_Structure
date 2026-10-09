# Autenticación: AuthService

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** Registro, confirmación, login con lockout, 2FA, refresh, logout, forgot/reset y cambio de email.  
> Índice general: `standards/00-INDEX.md`

### `AuthService` — `Infrastructure/Identity/AuthService.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    SessionManager sessions,
    ITokenService tokenService,
    AccountEmails emails,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // No revelar si el email existe: se responde igual y se avisa al dueño real por email.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            await emails.SendAccountExistsAsync(email, ct);
            return Result.Success();
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Error.FromIdentity(created.Errors);

        await userManager.AddToRoleAsync(user, AppRoles.User);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emails.SendConfirmationAsync(user, token, ct);

        logger.LogInformation("Usuario registrado {UserId}", user.Id);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        if (user.EmailConfirmed)
            return Result.Success();

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Result.Success() : AuthErrors.InvalidToken;
    }

    public async Task<Result> ResendConfirmationAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false, IsActive: true })
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendConfirmationAsync(user, token, ct);
        }
        return Result.Success();   // siempre éxito: no revela existencia
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return AuthErrors.InvalidCredentials;

        if (await userManager.IsLockedOutAsync(user))
            return AuthErrors.LockedOut;

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);   // cuenta intentos → lockout automático
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        // Estas verificaciones van DESPUÉS del password para no revelar estado de cuentas ajenas.
        if (!user.IsActive)
            return AuthErrors.AccountDisabled;

        if (userManager.Options.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
            return AuthErrors.EmailNotConfirmed;

        await userManager.ResetAccessFailedCountAsync(user);

        if (user.TwoFactorEnabled)
            return LoginResponse.TwoFactorRequired(tokenService.CreateTwoFactorChallengeToken(user.Id));

        return LoginResponse.Authenticated(await CompleteLoginAsync(user, ct));
    }

    public async Task<Result<AuthTokens>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct)
    {
        var userId = await tokenService.ValidateTwoFactorChallengeTokenAsync(request.TwoFactorToken);
        if (userId is null)
            return AuthErrors.InvalidTwoFactorToken;

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.TwoFactorEnabled)
            return AuthErrors.InvalidTwoFactorToken;

        if (!user.IsActive)
            return AuthErrors.AccountDisabled;

        if (await userManager.IsLockedOutAsync(user))
            return AuthErrors.LockedOut;

        var valid = request.IsRecoveryCode
            ? (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code.Trim())).Succeeded
            : await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider,
                request.Code.Replace(" ", string.Empty).Replace("-", string.Empty));

        if (!valid)
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidTwoFactorCode;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CompleteLoginAsync(user, ct);
    }

    public Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
        => sessions.RotateAsync(request.RefreshToken, ct);

    public async Task<Result> LogoutAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        await sessions.RevokeByRawTokenAsync(request.RefreshToken, ct);
        return Result.Success();   // idempotente
    }

    public async Task<Result> ForgotPasswordAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { IsActive: true })
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user, token, ct);
        }
        return Result.Success();   // misma respuesta exista o no el email
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? AuthErrors.InvalidToken
                : Error.FromIdentity(result.Errors);

        // Quien recibió el enlace en su buzón demostró ser dueño del email.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await sessions.RevokeAllAsync(user.Id, "Password reset", ct);
        await emails.SendSecurityNoticeAsync(user.Email!, "Tu contraseña fue restablecida. Si no fuiste tú, contacta soporte.", ct);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        var oldEmail = user.Email;
        var changed = await userManager.ChangeEmailAsync(user, request.NewEmail, token);
        if (!changed.Succeeded)
            return AuthErrors.InvalidToken;

        // El UserName se mantiene igual al email.
        var renamed = await userManager.SetUserNameAsync(user, request.NewEmail);
        if (!renamed.Succeeded)
            return Error.FromIdentity(renamed.Errors);

        await sessions.RevokeAllAsync(user.Id, "Email changed", ct);
        if (oldEmail is not null)
            await emails.SendSecurityNoticeAsync(oldEmail, $"El email de tu cuenta cambió a {request.NewEmail}.", ct);

        return Result.Success();
    }

    private async Task<AuthTokens> CompleteLoginAsync(AppUser user, CancellationToken ct)
    {
        user.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);
        return await sessions.IssueTokensAsync(user, familyId: null, ct);
    }
}
```
