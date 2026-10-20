using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace NetCoreApiKit;

/// <summary>Una tabla del modelo que ya existe en la base pero le faltan columnas.</summary>
public sealed record MissingColumn(string Table, string Column, string Sql);

/// <summary>
/// Qué hay que hacer para que una base existente quede como la necesita el proyecto, sin tocar lo que ya está:
/// crear las tablas que faltan, agregar las columnas que faltan y registrar la migración InitialCreate como aplicada.
/// </summary>
public sealed class SchemaPlan
{
    public required IReadOnlyList<string> ExpectedTables { get; init; }
    public required IReadOnlyList<string> ExistingTables { get; init; }
    public required IReadOnlyList<string> MissingTables { get; init; }
    public required IReadOnlyList<MissingColumn> MissingColumns { get; init; }
    public required IReadOnlyList<string> Statements { get; init; }      // en orden: tablas, columnas, historial
    public bool NothingToCreate => MissingTables.Count == 0 && MissingColumns.Count == 0;
}

public static partial class SchemaInspector
{
    /// <summary>Tablas estándar de ASP.NET Core Identity (conservan su nombre AspNet* también en PostgreSQL).</summary>
    public static readonly string[] IdentityTables =
        ["AspNetRoles", "AspNetUsers", "AspNetRoleClaims", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserRoles", "AspNetUserTokens"];

    /// <summary>Tablas y columnas del esquema público (PostgreSQL) o dbo (SQL Server). Vacío si la base no existe.</summary>
    public static async Task<Dictionary<string, HashSet<string>>> ListTablesAsync(string database, string connectionString)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var connection = Open(database, connectionString);
        try
        {
            await connection.OpenAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or SqlException)
        {
            return result;   // la base todavía no existe
        }

        await using var command = connection.CreateCommand();
        command.CommandText = database == "postgresql"
            ? "SELECT c.table_name, c.column_name FROM information_schema.columns c JOIN information_schema.tables t " +
              "ON t.table_schema = c.table_schema AND t.table_name = c.table_name " +
              "WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE'"
            : "SELECT c.TABLE_NAME, c.COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS c JOIN INFORMATION_SCHEMA.TABLES t " +
              "ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME " +
              "WHERE c.TABLE_SCHEMA = 'dbo' AND t.TABLE_TYPE = 'BASE TABLE'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (!result.TryGetValue(table, out var columns))
                result[table] = columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            columns.Add(reader.GetString(1));
        }
        return result;
    }

    /// <summary>
    /// Compara el script real de la migración inicial (<c>dotnet ef migrations script</c>) con lo que hay en la base
    /// y arma las sentencias mínimas. No borra ni modifica nada existente.
    /// </summary>
    public static async Task<SchemaPlan> BuildPlanAsync(ProjectChoices choices)
    {
        var scriptFile = Path.Combine(Path.GetTempPath(), $"kit-initial-{Guid.NewGuid():N}.sql");
        try
        {
            var env = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ConnectionStrings__Default"] = choices.ConnectionString!
            };
            var result = await Shell.RunAsync("dotnet",
                ["ef", "migrations", "script", "-p", $"src/{choices.Name}.Api", "-o", scriptFile], choices.OutputDirectory, env);
            if (!result.Ok)
                throw new StepFailedException("No se pudo generar el script de la migración inicial.", result.Tail());
            var script = await File.ReadAllTextAsync(scriptFile);
            var existing = await ListTablesAsync(choices.Database, choices.ConnectionString!);
            return Plan(choices.Database, script, existing);
        }
        finally
        {
            File.Delete(scriptFile);
        }
    }

    public static SchemaPlan Plan(string database, string script, Dictionary<string, HashSet<string>> existing)
    {
        var statements = Split(database, script);
        var createTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var statement in statements)
        {
            var match = CreateTableRegex().Match(statement);
            if (match.Success && !IsHistory(Ident(match)))
                createTables[Ident(match)] = statement;
        }

        var expected = createTables.Keys.ToList();
        var missingTables = expected.Where(t => !existing.ContainsKey(t)).ToList();
        var missingColumns = new List<MissingColumn>();
        foreach (var table in expected.Where(existing.ContainsKey))
        {
            foreach (var (column, definition) in Columns(createTables[table]))
            {
                if (!existing[table].Contains(column))
                    missingColumns.Add(new MissingColumn(table, column, AddColumnSql(database, table, column, definition)));
            }
        }

        var historyExists = existing.Keys.Any(IsHistory);
        var plan = new List<string>();
        var historyInsert = statements.LastOrDefault(s => InsertHistoryRegex().IsMatch(s));
        foreach (var statement in statements)
        {
            if (IsTransactionControl(statement))
                continue;
            if (CreateTableRegex().Match(statement) is { Success: true } create)
            {
                var table = Ident(create);
                if (IsHistory(table) ? !historyExists : missingTables.Contains(table, StringComparer.OrdinalIgnoreCase))
                    plan.Add(statement);
                continue;
            }
            if (TargetTable(statement) is { } target && !IsHistory(target))
            {
                if (missingTables.Contains(target, StringComparer.OrdinalIgnoreCase))
                    plan.Add(statement);   // índices y restricciones de las tablas nuevas
                continue;
            }
        }

        // Columnas faltantes después de crear tablas; el registro de la migración al final.
        var columnIndex = plan.FindLastIndex(s => CreateTableRegex().IsMatch(s) || TargetTable(s) is not null) + 1;
        plan.InsertRange(columnIndex, missingColumns.Select(c => c.Sql));
        if (historyInsert is not null)
            plan.Add(historyInsert);

        return new SchemaPlan
        {
            ExpectedTables = expected,
            ExistingTables = expected.Where(existing.ContainsKey).ToList(),
            MissingTables = missingTables,
            MissingColumns = missingColumns,
            Statements = plan
        };
    }

    /// <summary>Filas aproximadas por tabla (estadísticas del motor: rápido aunque la tabla sea grande).</summary>
    public static async Task<Dictionary<string, long>> EstimateRowsAsync(string database, string connectionString)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var connection = Open(database, connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = database == "postgresql"
            ? "SELECT c.relname, GREATEST(c.reltuples, 0)::bigint FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
              "WHERE n.nspname = 'public' AND c.relkind = 'r'"
            : "SELECT t.name, SUM(p.rows) FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1) " +
              "WHERE SCHEMA_NAME(t.schema_id) = 'dbo' GROUP BY t.name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = Convert.ToInt64(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
        if (database == "postgresql" && result.Values.All(v => v == 0))
        {
            // Sin ANALYZE las estadísticas de PostgreSQL pueden estar en 0: se cuenta exacto (solo tablas chicas o nuevas).
            await reader.CloseAsync();
            foreach (var table in result.Keys.ToList())
            {
                await using var count = connection.CreateCommand();
                count.CommandText = $"SELECT count(*) FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
                result[table] = Convert.ToInt64(await count.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return result;
    }

    /// <summary>Nombre del esquema de respaldo: kit_backup_aaaammdd_hhmmss (UTC).</summary>
    public static string BackupSchemaName() => FormattableString.Invariant($"kit_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

    /// <summary>
    /// Mueve las tablas al esquema de respaldo SIN copiar ni borrar nada: conservan datos, índices, restricciones y relaciones.
    /// Después la migración crea tablas nuevas y vacías con los nombres originales.
    /// </summary>
    public static List<string> MoveToBackupStatements(string database, string schema, IEnumerable<string> tables)
    {
        var statements = new List<string>
        {
            database == "postgresql" ? $"CREATE SCHEMA \"{schema}\";" : $"EXEC('CREATE SCHEMA [{schema}]');"
        };
        foreach (var table in tables)
            statements.Add(database == "postgresql"
                ? $"ALTER TABLE public.\"{table}\" SET SCHEMA \"{schema}\";"
                : $"ALTER SCHEMA [{schema}] TRANSFER [dbo].[{table}];");
        return statements;
    }

    /// <summary>Copia completa (datos) de las tablas al esquema de respaldo; los originales quedan donde están.</summary>
    public static List<string> CopyToBackupStatements(string database, string schema, IEnumerable<string> tables)
    {
        var statements = new List<string>
        {
            database == "postgresql" ? $"CREATE SCHEMA \"{schema}\";" : $"EXEC('CREATE SCHEMA [{schema}]');"
        };
        foreach (var table in tables)
            statements.Add(database == "postgresql"
                ? $"CREATE TABLE \"{schema}\".\"{table}\" AS TABLE public.\"{table}\";"
                : $"SELECT * INTO [{schema}].[{table}] FROM [dbo].[{table}];");
        return statements;
    }

    /// <summary>Ejecuta el plan en una transacción: si algo falla, no queda nada a medias.</summary>
    public static async Task ApplyAsync(string database, string connectionString, IReadOnlyList<string> statements)
    {
        await using var connection = Open(database, connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var sql in statements)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    // ------------------------------------------------------------ análisis del script

    private static List<string> Split(string database, string script)
    {
        var parts = database == "sqlserver"
            ? Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline)
            : Regex.Split(script, @";\s*$", RegexOptions.Multiline);
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0)
            .Select(p => database == "postgresql" && !p.EndsWith(';') ? p + ";" : p)
            .ToList();
    }

    private static bool IsTransactionControl(string statement)
        => Regex.IsMatch(statement.Trim().TrimEnd(';'), @"^(START TRANSACTION|BEGIN TRANSACTION|BEGIN|COMMIT)$", RegexOptions.IgnoreCase);

    private static bool IsHistory(string table) => table.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase);

    private static string Ident(Match match)
        => new[] { match.Groups["b"], match.Groups["q"], match.Groups["p"] }.First(g => g.Success).Value;

    private static string? TargetTable(string statement)
    {
        var match = IndexOrAlterRegex().Match(statement);
        return match.Success ? Ident(match) : null;
    }

    /// <summary>Columnas de un CREATE TABLE: (nombre, definición sin la coma final). Ignora CONSTRAINT/PRIMARY KEY.</summary>
    private static IEnumerable<(string Column, string Definition)> Columns(string createTable)
    {
        var start = createTable.IndexOf('(');
        var end = createTable.LastIndexOf(')');
        if (start < 0 || end <= start)
            yield break;
        foreach (var raw in createTable[(start + 1)..end].Split('\n'))
        {
            var line = raw.Trim().TrimEnd(',');
            if (line.Length == 0 || line.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                continue;
            var match = ColumnRegex().Match(line);
            if (match.Success)
                yield return (Ident(match), line);
        }
    }

    /// <summary>ALTER TABLE ... ADD con la misma definición; si es NOT NULL sin DEFAULT, agrega un valor por defecto para las filas que ya existen.</summary>
    private static string AddColumnSql(string database, string tableName, string column, string definition)
    {
        var table = database == "postgresql" ? $"\"{tableName}\"" : $"[{tableName}]";
        var sql = definition;
        if (sql.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)
            && !sql.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase)
            && !sql.Contains("IDENTITY", StringComparison.OrdinalIgnoreCase)
            && !sql.Contains("GENERATED", StringComparison.OrdinalIgnoreCase))
            sql += " DEFAULT " + DefaultFor(database, column, sql);
        return database == "postgresql" ? $"ALTER TABLE {table} ADD COLUMN {sql};" : $"ALTER TABLE {table} ADD {sql};";
    }

    private static string DefaultFor(string database, string column, string definition)
    {
        var type = definition.ToLowerInvariant();
        var activeFlag = column.Replace("_", "", StringComparison.Ordinal).Equals("isactive", StringComparison.OrdinalIgnoreCase);
        if (database == "postgresql")
        {
            if (type.Contains(" boolean")) return activeFlag ? "TRUE" : "FALSE";
            if (type.Contains(" timestamp") || type.Contains(" date")) return "now()";
            if (type.Contains(" uuid")) return "gen_random_uuid()";
            if (Regex.IsMatch(type, @" (integer|bigint|smallint|numeric|double|real)")) return "0";
            return "''";
        }
        if (type.Contains(" bit")) return activeFlag ? "CAST(1 AS bit)" : "CAST(0 AS bit)";
        if (type.Contains(" datetime") || type.Contains(" date")) return "SYSUTCDATETIME()";
        if (type.Contains(" uniqueidentifier")) return "NEWID()";
        if (Regex.IsMatch(type, @" (int|bigint|smallint|tinyint|decimal|float|real)")) return "0";
        return "N''";
    }

    private static DbConnection Open(string database, string connectionString)
        => database == "postgresql" ? new NpgsqlConnection(connectionString) : new SqlConnection(connectionString);

    private const string IdentPattern = @"(?:\[(?<b>[^\]]+)\]|""(?<q>[^""]+)""|(?<p>[A-Za-z_][\w$]*))";

    [GeneratedRegex(@"CREATE TABLE (?:IF NOT EXISTS )?(?:" + IdentPattern + @"\.)?" + IdentPattern + @"\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex CreateTableRegex();

    [GeneratedRegex(@"^\s*(?:CREATE (?:UNIQUE )?INDEX\s+\S+\s+ON|ALTER TABLE|INSERT INTO)\s+(?:" + IdentPattern + @"\.)?" + IdentPattern, RegexOptions.IgnoreCase)]
    private static partial Regex IndexOrAlterRegex();

    [GeneratedRegex(@"^" + IdentPattern + @"\s+\S+", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnRegex();

    [GeneratedRegex(@"INSERT INTO\s+(?:\[__EFMigrationsHistory\]|""__EFMigrationsHistory"")", RegexOptions.IgnoreCase)]
    private static partial Regex InsertHistoryRegex();

    [GeneratedRegex(@"VALUES\s*\(\s*N?'([^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex MigrationIdRegex();
}
