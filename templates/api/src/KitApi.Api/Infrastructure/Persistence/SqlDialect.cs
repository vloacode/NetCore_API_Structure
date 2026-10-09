#if (postgresql)
using System.Text.RegularExpressions;
#endif

namespace KitApi.Infrastructure.Persistence;

public static partial class SqlDialect
{
#if (sqlserver)
    public const string Provider = "SqlServer";
#endif
#if (postgresql)
    public const string Provider = "PostgreSQL";
#endif

#if (sqlserver)
    public const string True = "1";
#endif
#if (postgresql)
    public const string True = "true";
#endif
#if (sqlserver)
    public const string False = "0";
#endif
#if (postgresql)
    public const string False = "false";
#endif

    /// <summary>Nombre de columna tal como queda en la BD (PostgreSQL usa snake_case con EFCore.NamingConventions).</summary>
#if (sqlserver)
    public static string Column(string propertyName) => $"[{propertyName}]";
#endif
#if (postgresql)
    public static string Column(string propertyName) => $"\"{ToSnakeCase(propertyName)}\"";
#endif

    /// <summary>Filtro de índices únicos que ignoran los registros eliminados (soft delete).</summary>
    public static string NotDeleted => $"{Column("IsDeleted")} = {False}";

#if (postgresql)
    private static string ToSnakeCase(string name) => SnakeCaseRegex().Replace(name, "$1_$2").ToLowerInvariant();
#endif

#if (postgresql)
    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseRegex();
#endif
}
