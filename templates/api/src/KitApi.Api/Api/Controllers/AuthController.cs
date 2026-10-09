using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitApi.Application.Features.Auth;

namespace KitApi.Api.Controllers;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        => HandleResult(await auth.RegisterAsync(request, ct));

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailAsync(request, ct));

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ResendConfirmationAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginAsync(request, ct));

    [HttpPost("login/2fa")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginWithTwoFactorAsync(request, ct));

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.LogoutAsync(request, ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ForgotPasswordAsync(request, ct));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        => HandleResult(await auth.ResetPasswordAsync(request, ct));

    [HttpPost("confirm-email-change")]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailChangeAsync(request, ct));
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
