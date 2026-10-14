using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Npgsql;
using Spectre.Console;

namespace NetCoreApiKit;

/// <summary>Resultado de probar una conexión: si se pudo entrar al servidor y si la base ya existe.</summary>
public sealed record ConnectionCheck(bool Ok, bool DatabaseExists, string Message);

/// <summary>Pide los datos de la base (máquina local o servidor), arma la cadena y la prueba antes de guardar nada.</summary>
public static class DatabaseSetup
{
    /// <summary>Pregunta paso a paso hasta tener una conexión que funcione, o el usuario decide omitir.</summary>
    public static async Task<string?> AskConnectionAsync(string database, string defaultDbName, bool isServer)
    {
        while (true)
        {
            var connection = database == "postgresql"
                ? AskPostgreSql(defaultDbName, isServer)
                : AskSqlServer(defaultDbName, isServer);

            ConnectionCheck check = null!;
            await AnsiConsole.Status().StartAsync("Probando la conexión…", async _ => check = await TestAsync(database, connection));
            if (check.Ok)
            {
                AnsiConsole.MarkupLine($"  [green]✔[/] {Markup.Escape(check.Message)}");
                return connection;
            }

            AnsiConsole.MarkupLine($"  [red]✘ No se pudo conectar:[/] {Markup.Escape(check.Message)}");
            var next = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("¿Qué hago?")
                .AddChoices("Corregir los datos", "Omitir (configurar la base después)"));
            if (next.StartsWith("Omitir", StringComparison.Ordinal))
                return null;
        }
    }

    private static string AskPostgreSql(string defaultDbName, bool isServer)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = AnsiConsole.Prompt(new TextPrompt<string>("  Host:").DefaultValue(isServer ? "" : "localhost").AllowEmpty()),
            Port = AnsiConsole.Prompt(new TextPrompt<int>("  Puerto:").DefaultValue(5432)),
            Database = AnsiConsole.Prompt(new TextPrompt<string>("  Nombre de la base:").DefaultValue(defaultDbName)),
            Username = AnsiConsole.Prompt(new TextPrompt<string>("  Usuario:").DefaultValue("postgres")),
            Password = AnsiConsole.Prompt(new TextPrompt<string>("  Contraseña:").Secret().AllowEmpty())
        };
        builder.SslMode = AnsiConsole.Prompt(new SelectionPrompt<SslMode>()
            .Title("  SSL:")
            .AddChoices(isServer ? [SslMode.Require, SslMode.Prefer, SslMode.Disable] : [SslMode.Disable, SslMode.Prefer, SslMode.Require]));
        return builder.ConnectionString;
    }

    private static string AskSqlServer(string defaultDbName, bool isServer)
    {
        var defaultServer = isServer ? "" : OperatingSystem.IsWindows() ? @"(localdb)\MSSQLLocalDB" : "localhost,1433";
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = AnsiConsole.Prompt(new TextPrompt<string>("  Servidor (host, host,puerto o host\\instancia):").DefaultValue(defaultServer)),
            InitialCatalog = AnsiConsole.Prompt(new TextPrompt<string>("  Nombre de la base:").DefaultValue(defaultDbName))
        };
        var windowsAuth = OperatingSystem.IsWindows()
                          && AnsiConsole.Confirm("  ¿Autenticación de Windows? (sin usuario ni contraseña)", !isServer);
        if (windowsAuth)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = AnsiConsole.Prompt(new TextPrompt<string>("  Usuario:").DefaultValue("sa"));
            builder.Password = AnsiConsole.Prompt(new TextPrompt<string>("  Contraseña:").Secret());
        }
        builder.TrustServerCertificate = AnsiConsole.Confirm("  ¿Confiar en el certificado del servidor? (TrustServerCertificate; sí para desarrollo)", !isServer);
        return builder.ConnectionString;
    }

    /// <summary>
    /// Entra a la base del sistema del servidor (postgres / master) para validar host y credenciales sin depender
    /// de que la base del proyecto exista, y luego revisa si existe.
    /// </summary>
    public static async Task<ConnectionCheck> TestAsync(string database, string connectionString)
    {
        try
        {
            if (database == "postgresql")
            {
                var target = new NpgsqlConnectionStringBuilder(connectionString);
                var admin = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Timeout = 8 };
                await using var connection = new NpgsqlConnection(admin.ConnectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
                command.Parameters.AddWithValue("name", target.Database ?? "");
                var exists = await command.ExecuteScalarAsync() is not null;
                return new ConnectionCheck(true, exists, exists
                    ? $"Conectado a {target.Host}:{target.Port}; la base '{target.Database}' ya existe."
                    : $"Conectado a {target.Host}:{target.Port}; la base '{target.Database}' no existe todavía (se crea con la migración).");
            }
            else
            {
                var target = new SqlConnectionStringBuilder(connectionString);
                var admin = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", ConnectTimeout = 8 };
                await using var connection = new SqlConnection(admin.ConnectionString);
                await connection.OpenAsync();
                await using var command = new SqlCommand("SELECT DB_ID(@name)", connection);
                command.Parameters.AddWithValue("@name", target.InitialCatalog);
                var exists = await command.ExecuteScalarAsync() is not (null or DBNull);
                return new ConnectionCheck(true, exists, exists
                    ? $"Conectado a {target.DataSource}; la base '{target.InitialCatalog}' ya existe."
                    : $"Conectado a {target.DataSource}; la base '{target.InitialCatalog}' no existe todavía (se crea con la migración).");
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or SqlException or InvalidOperationException or ArgumentException or SocketException)
        {
            return new ConnectionCheck(false, false, ex.Message);
        }
    }

    /// <summary>Espera a que la base del contenedor acepte conexiones (SQL Server tarda más en arrancar).</summary>
    public static async Task<ConnectionCheck> WaitForAsync(string database, string connectionString, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        ConnectionCheck last;
        do
        {
            last = await TestAsync(database, connectionString);
            if (last.Ok)
                return last;
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        while (DateTime.UtcNow < until);
        return last;
    }

    /// <summary>Primer puerto libre desde <paramref name="preferred"/> (por si ya hay un motor instalado en esa máquina).</summary>
    public static int FreePort(int preferred)
    {
        for (var port = preferred; port < preferred + 50; port++)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                return port;
            }
            catch (SocketException)
            {
            }
        }
        return preferred;
    }
}
