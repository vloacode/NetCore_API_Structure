#if (useApiKey)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
#if (security)
using KitApi.Api.Authorization;
#endif
using KitApi.Api.Controllers;
using KitApi.Application.Common.Paging;
#if (security)
using KitApi.Application.Common.Security;
#endif

namespace KitApi.Features.KitEntities;

public sealed class KitEntitiesController(IKitEntityService service) : ApiControllerBase
{
#if (security)
    [HttpGet, HasPermission(Permissions.KitEntities.Read)]
#else
    [HttpGet]
#endif
    [ProducesResponseType<PagedResult<KitEntityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] KitEntityFilter filter, CancellationToken ct)
        => HandleResult(await service.GetPagedAsync(filter, ct));

#if (security)
    [HttpGet("{id:int}"), HasPermission(Permissions.KitEntities.Read)]
#else
    [HttpGet("{id:int}")]
#endif
    [ProducesResponseType<KitEntityDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => HandleResult(await service.GetByIdAsync(id, ct));

#if (security)
    [HttpPost, HasPermission(Permissions.KitEntities.Write)]
#elif (useApiKey)
    [HttpPost, Authorize]
#else
    [HttpPost]
#endif
    [ProducesResponseType<KitEntityDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateKitEntityRequest request, CancellationToken ct)
        => HandleCreated(await service.CreateAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

#if (security)
    [HttpPut("{id:int}"), HasPermission(Permissions.KitEntities.Write)]
#elif (useApiKey)
    [HttpPut("{id:int}"), Authorize]
#else
    [HttpPut("{id:int}")]
#endif
    [ProducesResponseType<KitEntityDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateKitEntityRequest request, CancellationToken ct)
        => HandleResult(await service.UpdateAsync(id, request, ct));

#if (security)
    [HttpDelete("{id:int}"), HasPermission(Permissions.KitEntities.Delete)]
#elif (useApiKey)
    [HttpDelete("{id:int}"), Authorize]
#else
    [HttpDelete("{id:int}")]
#endif
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => HandleResult(await service.DeleteAsync(id, ct));
}
