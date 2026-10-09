using System.Text.Json;
using FluentValidation;
using KitApi.Application.Abstractions.Analytics;
using KitApi.Application.Common.Results;

namespace KitApi.Application.Features.Analytics;

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
