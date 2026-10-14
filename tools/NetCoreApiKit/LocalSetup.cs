using System.Text;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace NetCoreApiKit;

/// <summary>Deja el proyecto listo para correr: base (Docker), secretos, formato, build y migración inicial.</summary>
public static class LocalSetup
{
    /// <summary>Levanta la base (y Mailpit) con docker compose y devuelve la cadena de conexión hacia localhost.</summary>
    public static async Task<string> StartDockerDatabaseAsync(ProjectChoices choices, IReadOnlyDictionary<string, string> appSecrets, Action<string> status)
    {
        var password = Secrets.DatabasePassword();
        var port = DatabaseSetup.FreePort(choices.PostgreSql ? 5432 : 1433);
        var env = new StringBuilder("# Claves y puertos de docker compose (desarrollo). No se sube a git.\n");
        env.Append(choices.PostgreSql ? $"PG_PASSWORD={password}\n" : $"SA_PASSWORD={password}\n");
        env.Append(FormattableString.Invariant($"DB_PORT={port}\n"));
        // Mismos valores que los secretos de desarrollo: con `dotnet run` o con `docker compose up api`, el mismo admin.
        if (appSecrets.TryGetValue("Jwt:SigningKey", out var jwt))
            env.Append($"JWT_SIGNING_KEY={jwt}\n");
        if (appSecrets.TryGetValue("Seed:AdminPassword", out var admin))
            env.Append($"ADMIN_PASSWORD={admin}\n");
        if (appSecrets.TryGetValue("Analytics:HashKey", out var hash))
            env.Append($"ANALYTICS_HASH_KEY={hash}\n");
        await File.WriteAllTextAsync(Path.Combine(choices.OutputDirectory, ".env"), env.ToString());

        var services = new List<string> { "db" };
        if (choices.Smtp && DatabaseSetup.FreePort(8025) == 8025 && DatabaseSetup.FreePort(1025) == 1025)
            services.Add("mail");

        status(choices.PostgreSql ? "Levantando PostgreSQL en Docker…" : "Levantando SQL Server en Docker (la primera vez descarga ~1,5 GB)…");
        var up = await Shell.RunAsync("docker", ["compose", "up", "-d", .. services], choices.OutputDirectory);
        if (!up.Ok)
            throw new StepFailedException("docker compose no pudo levantar la base.", up.Tail());

        var connection = choices.PostgreSql
            ? new NpgsqlConnectionStringBuilder
            {
                Host = "localhost", Port = port, Database = $"{choices.LowerName}_db", Username = $"{choices.LowerName}_app", Password = password
            }.ConnectionString
            : new SqlConnectionStringBuilder
            {
                DataSource = FormattableString.Invariant($"localhost,{port}"), InitialCatalog = $"{choices.Name}Db",
                UserID = "sa", Password = password, TrustServerCertificate = true
            }.ConnectionString;

        status("Esperando a que la base acepte conexiones…");
        var check = await DatabaseSetup.WaitForAsync(choices.Database, connection, TimeSpan.FromSeconds(choices.PostgreSql ? 90 : 240));
        if (!check.Ok)
            throw new StepFailedException("La base en Docker no respondió a tiempo.", check.Message);
        return connection;
    }

    /// <summary>Valores sensibles generados (la conexión se agrega cuando se conoce).</summary>
    public static Dictionary<string, string> BuildSecrets(ProjectChoices choices, out string? adminPassword)
    {
        var values = new Dictionary<string, string>();
        adminPassword = null;
        if (choices.Security)
        {
            values["Jwt:SigningKey"] = Secrets.Key();
            adminPassword = Secrets.Password();
            values["Seed:AdminPassword"] = adminPassword;
        }
        if (choices.Analytics)
            values["Analytics:HashKey"] = Secrets.Key();
        return values;
    }

    public static async Task FormatAndBuildAsync(ProjectChoices choices)
    {
        await Shell.DotnetAsync(choices.OutputDirectory, "format", choices.SolutionFile);   // ordena los using según el nombre real
        var build = await Shell.DotnetAsync(choices.OutputDirectory, "build", choices.SolutionFile, "-nologo");
        if (!build.Ok)
            throw new StepFailedException("dotnet build falló.", build.Tail());
    }

    public static async Task AddInitialMigrationAsync(ProjectChoices choices)
    {
        var result = await Shell.DotnetAsync(choices.OutputDirectory, "ef", "migrations", "add", "InitialCreate",
            "-p", $"src/{choices.Name}.Api", "-o", "Infrastructure/Persistence/Migrations");
        if (!result.Ok)
            throw new StepFailedException("No se pudo crear la migración inicial.", result.Tail());
    }

    /// <summary>Aplica las migraciones. La conexión viaja por variable de entorno (no por argumentos).</summary>
    public static async Task ApplyMigrationsAsync(ProjectChoices choices)
    {
        var env = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ConnectionStrings__Default"] = choices.ConnectionString!
        };
        var result = await Shell.RunAsync("dotnet", ["ef", "database", "update", "-p", $"src/{choices.Name}.Api"],
            choices.OutputDirectory, env);
        if (!result.Ok)
            throw new StepFailedException("No se pudieron aplicar las migraciones.", result.Tail());
    }

    public static async Task<bool> InitGitAsync(ProjectChoices choices)
    {
        if (Directory.Exists(Path.Combine(choices.OutputDirectory, ".git")))
            return true;
        return (await Shell.RunAsync("git", ["init", "-q"], choices.OutputDirectory)).Ok;
    }
}
