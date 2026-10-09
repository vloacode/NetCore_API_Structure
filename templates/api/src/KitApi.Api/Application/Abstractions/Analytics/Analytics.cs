namespace KitApi.Application.Abstractions.Analytics;

public enum AnalyticsSource { Server, Client }

public interface IAnalyticsTracker
{
    /// <summary>Encola un evento de uso. No bloquea ni lanza: si la analítica está apagada o la cola llena, se descarta.</summary>
    void Track(string eventName, IReadOnlyDictionary<string, object?>? properties = null, AnalyticsSource source = AnalyticsSource.Server);
}

/// <summary>Catálogo de eventos (detalle y propósito de cada uno en docs/11-product-analytics.md).</summary>
public static class AnalyticsEvents
{
    // ---- Eventos del servidor: uno por hecho de negocio que interese medir ----
    // Formato "area.accion" en pasado, ej. "invoice.paid" (flujo analytics-event).

    // ---- Eventos que el frontend puede enviar por POST /api/analytics/events ----
    public const string PageViewed = "page.viewed";
    public const string SearchNoResults = "search.no_results";

    public static readonly IReadOnlySet<string> ClientAllowed = new HashSet<string>(StringComparer.Ordinal)
    {
        PageViewed,
        SearchNoResults
    };
}

/// <summary>Evita que lleguen datos personales o valores no aptos a la analítica.</summary>
public static class AnalyticsPropertyGuard
{
    public const int MaxProperties = 25;
    public const int MaxStringLength = 200;

    private static readonly string[] _sensitiveFragments = ["email", "mail", "password", "pass", "token", "secret", "phone", "card", "ssn"];
    private static readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        { "name", "firstname", "lastname", "fullname", "address", "ip", "ipaddress", "username" };

    public static bool IsSensitiveKey(string key)
        => _sensitiveKeys.Contains(key) || _sensitiveFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedValue(object? value) => value switch
    {
        null or bool or int or long or decimal or double or float or DateTime or DateTimeOffset or Guid => true,
        string s => s.Length <= MaxStringLength,
        _ => false
    };

    /// <summary>Devuelve solo las propiedades permitidas y la lista de claves descartadas.</summary>
    public static Dictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?>? properties, out List<string> rejected)
    {
        rejected = [];
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (properties is null) return result;

        foreach (var (key, value) in properties)
        {
            if (result.Count >= MaxProperties || IsSensitiveKey(key) || !IsAllowedValue(value))
                rejected.Add(key);
            else
                result[key] = value;
        }
        return result;
    }
}
