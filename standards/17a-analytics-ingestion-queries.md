# Analítica de producto: eventos del frontend, consultas y pruebas

> **Aplica a:** Solo si `Capabilities` incluye `product-analytics` (núcleo en `standards/17`).  
> **Propósito:** Endpoint para que el frontend envíe eventos de uso, consultas SQL de KPIs y tests de integración.  
> Índice general: `standards/00-INDEX.md`

## Eventos del frontend — `Application/Features/Analytics/AnalyticsContracts.cs`
El frontend solo puede enviar eventos de la lista blanca `AnalyticsEvents.ClientAllowed`. Las reglas de privacidad se validan aquí (400) y otra vez en el tracker.
```csharp
using System.Text.Json;
using FluentValidation;
using {Project}.Application.Abstractions.Analytics;
using {Project}.Application.Common.Results;

namespace {Project}.Application.Features.Analytics;

public sealed record ClientEventDto(string Name, Dictionary<string, JsonElement>? Properties);
public sealed record ClientEventsRequest(IReadOnlyList<ClientEventDto> Events);

public sealed class ClientEventsRequestValidator : AbstractValidator<ClientEventsRequest>
{
    public const int MaxEvents = 50;

    public ClientEventsRequestValidator()
    {
        RuleFor(x => x.Events).NotEmpty().Must(e => e.Count <= MaxEvents).WithMessage($"Máximo {MaxEvents} eventos por envío.");
        RuleForEach(x => x.Events).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).Must(n => AnalyticsEvents.ClientAllowed.Contains(n)).WithMessage("Evento no permitido.");
            e.RuleFor(x => x.Properties).Must(p => p is null || p.Count <= AnalyticsPropertyGuard.MaxProperties)
                .WithMessage($"Máximo {AnalyticsPropertyGuard.MaxProperties} propiedades por evento.");
            e.RuleFor(x => x.Properties).Must(p => p is null || !p.Keys.Any(AnalyticsPropertyGuard.IsSensitiveKey))
                .WithMessage("No se permiten propiedades con datos personales.");
        });
    }
}

public interface IAnalyticsIngestionService
{
    Result Ingest(ClientEventsRequest request);
}

public sealed class AnalyticsIngestionService(IAnalyticsTracker tracker) : IAnalyticsIngestionService
{
    public Result Ingest(ClientEventsRequest request)
    {
        foreach (var e in request.Events)
            tracker.Track(e.Name, e.Properties?.ToDictionary(p => p.Key, p => ToValue(p.Value)), AnalyticsSource.Client);
        return Result.Success();
    }

    private static object? ToValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}
```

## Controller — `Api/Controllers/AnalyticsController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using {Project}.Application.Features.Analytics;
using {Project}.Infrastructure.Analytics;

namespace {Project}.Api.Controllers;

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
```

## Contrato con el frontend
- `POST /api/analytics/events` con `{ "events": [ { "name": "search.no_results", "properties": { "term_length": 7 } } ] }`.
- Máximo 50 eventos por envío: el frontend junta eventos y los manda cada pocos segundos o al cambiar de pantalla.
- Header `X-Anonymous-Id`: un GUID aleatorio que el frontend genera una vez y guarda (por ejemplo en `localStorage`). Permite contar usuarios únicos sin identificarlos.
- Respuestas: **202** aceptado, **400** evento no permitido o con datos personales (`code = Validation.Failed`), **429** demasiados envíos.
- Si el usuario rechazó la analítica (consentimiento), el frontend no envía eventos ni el header.

## Consultas de KPIs (ejemplos)
Eventos por día de las últimas 4 semanas:
```sql
-- SQL Server
SELECT CAST(OccurredAt AS date) AS Day, Name, COUNT(*) AS Events, COUNT(DISTINCT AnonymousId) AS Users
FROM AnalyticsEvents
WHERE OccurredAt >= DATEADD(day, -28, SYSUTCDATETIME())
GROUP BY CAST(OccurredAt AS date), Name
ORDER BY Day, Name;

-- PostgreSQL
SELECT occurred_at::date AS day, name, COUNT(*) AS events, COUNT(DISTINCT anonymous_id) AS users
FROM analytics_events
WHERE occurred_at >= now() - interval '28 days'
GROUP BY occurred_at::date, name
ORDER BY day, name;
```

Embudo de dos pasos (cuántos de los que hicieron A hicieron después B):
```sql
-- PostgreSQL (en SQL Server, la misma lógica con OccurredAt / AnonymousId)
WITH a AS (SELECT anonymous_id, MIN(occurred_at) AS at FROM analytics_events WHERE name = 'user.registered' GROUP BY anonymous_id),
     b AS (SELECT anonymous_id, MIN(occurred_at) AS at FROM analytics_events WHERE name = '{entity}.created' GROUP BY anonymous_id)
SELECT COUNT(a.anonymous_id) AS step_a, COUNT(b.anonymous_id) AS step_b
FROM a LEFT JOIN b ON b.anonymous_id = a.anonymous_id AND b.at >= a.at;
```

Leer una propiedad del JSON: SQL Server `JSON_VALUE(Properties, '$.plan')`; PostgreSQL `properties->>'plan'`.

Estas consultas se corren en una herramienta SQL o se conectan a un tablero (Metabase, Grafana…) **con un usuario de solo lectura**. No se crean endpoints de reportes: eso sería BI, fuera de esta capacidad.

## Tests de integración — `tests/{Project}.IntegrationTests/ProductAnalyticsTests.cs`
El despacho es asíncrono, así que los tests esperan con sondeo (hasta ~5 s) a que el evento aparezca en la BD.
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using {Project}.Application.Abstractions.Analytics;
using {Project}.Infrastructure.Analytics;
using {Project}.Infrastructure.Persistence;

namespace {Project}.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ProductAnalyticsTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Track_ServerEvent_IsStoredWithoutPersonalData()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IAnalyticsTracker>().Track("test.server_event",
                new Dictionary<string, object?> { ["plan"] = "pro", ["email"] = "someone@example.com" });
        }

        var stored = await WaitForEventAsync("test.server_event");

        stored.Source.ShouldBe("server");
        stored.Properties.ShouldContain("plan");
        stored.Properties.ShouldNotContain("email");
    }

    [Fact]
    public async Task PostEvents_FromClient_Returns202AndStoresOnlyPseudonym()
    {
        var anonymousId = Guid.NewGuid().ToString();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AnalyticsTracker.AnonymousIdHeader, anonymousId);

        var response = await client.PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = AnalyticsEvents.SearchNoResults, properties = new { term_length = 7 } } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var stored = await WaitForEventAsync(AnalyticsEvents.SearchNoResults);
        stored.Source.ShouldBe("client");
        stored.AnonymousId.ShouldNotBeNullOrEmpty();
        stored.AnonymousId.ShouldNotBe(anonymousId);   // seudónimo, nunca el valor real
    }

    [Fact]
    public async Task PostEvents_WithPersonalData_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = AnalyticsEvents.PageViewed, properties = new { email = "a@b.com" } } } },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostEvents_WithUnknownEvent_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/analytics/events",
            new { events = new[] { new { name = "admin.deleted_everything" } } }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<AnalyticsEvent> WaitForEventAsync(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<AnalyticsEvent>()
                .AsNoTracking().FirstOrDefaultAsync(e => e.Name == name, ct);
            if (stored is not null)
                return stored;
            await Task.Delay(100, ct);
        }
        throw new ShouldAssertException($"No se registró el evento {name}");
    }
}
```
`ApiFactory` (`standards/12`) debe configurar `Analytics:HashKey` cuando la capacidad está activa.
