using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitApi.Application.Features.Analytics;
using KitApi.Infrastructure.Analytics;

namespace KitApi.Api.Controllers;

/// <summary>
/// Eventos de uso del frontend. Anónimo a propósito (cubre pantallas previas al login): con seguridad, si llega un token
/// válido el evento se asocia al usuario; si no, al X-Anonymous-Id. En ambos casos se guarda solo el seudónimo.
/// </summary>
public sealed class AnalyticsController(IAnalyticsIngestionService ingestion) : ApiControllerBase
{
    [HttpPost("events")]
    [EnableRateLimiting(AnalyticsServiceCollectionExtensions.RateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult PostEvents(ClientEventsRequest request)
    {
        var result = ingestion.Ingest(request);
        return result.IsSuccess ? Accepted() : ToProblem(result.Error);
    }
}
