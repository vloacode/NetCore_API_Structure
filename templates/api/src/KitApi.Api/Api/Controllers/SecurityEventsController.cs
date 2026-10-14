using Microsoft.AspNetCore.Mvc;
using KitApi.Api.Authorization;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Security;
using KitApi.Application.Features.Auth;

namespace KitApi.Api.Controllers;

/// <summary>Bitácora de seguridad: logins, bloqueos, cambios de contraseña, roles y permisos (standards/05).</summary>
[Route("api/security-events")]
public sealed class SecurityEventsController(ISecurityEventService events) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Audit.Read)]
    [ProducesResponseType<PagedResult<SecurityEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] SecurityEventFilter filter, CancellationToken ct)
        => HandleResult(await events.GetPagedAsync(filter, ct));
}
