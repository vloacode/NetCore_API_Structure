using System.Globalization;

namespace KitApi.Domain.Common;

/// <summary>
/// Conversión de EntityKey (Guid, int o long) a texto y de vuelta, sin depender de la cultura del servidor:
/// Identity (FindByIdAsync), el claim sub del JWT y los links de los emails.
/// </summary>
public static class EntityKeys
{
    public static string ToInvariantString(this EntityKey key) => key.ToString(null, CultureInfo.InvariantCulture);

    public static bool TryParse(string? value, out EntityKey key) => EntityKey.TryParse(value, CultureInfo.InvariantCulture, out key);
}
