using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitApi.Application.Features.Auth;

namespace KitApi.Api.Controllers;

[Authorize]
public sealed class AccountController(IAccountService account) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => HandleResult(await account.GetProfileAsync(ct));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => HandleResult(await account.UpdateProfileAsync(request, ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => HandleResult(await account.ChangePasswordAsync(request, ct));

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
        => HandleResult(await account.RequestEmailChangeAsync(request, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct) => HandleResult(await account.GetSessionsAsync(ct));

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
        => HandleResult(await account.RevokeSessionAsync(sessionId, ct));

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) => HandleResult(await account.LogoutAllAsync(ct));

    [HttpGet("2fa")]
    public async Task<IActionResult> TwoFactorStatus(CancellationToken ct) => HandleResult(await account.GetTwoFactorStatusAsync(ct));

    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupAuthenticator(CancellationToken ct) => HandleResult(await account.SetupAuthenticatorAsync(ct));

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct)
        => HandleResult(await account.EnableTwoFactorAsync(request, ct));

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
        => HandleResult(await account.DisableTwoFactorAsync(request, ct));

    [HttpPost("2fa/recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodes(CancellationToken ct)
        => HandleResult(await account.RegenerateRecoveryCodesAsync(ct));
}
