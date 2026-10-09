using System.Data.Common;
#if (useApiKey)
using System.Security.Cryptography;
using System.Text;
#endif
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
#if (sqlserver)
using Microsoft.Data.SqlClient;
#endif
using Microsoft.Extensions.DependencyInjection;
#if (postgresql)
using Npgsql;
#endif
using Respawn;
#if (sqlserver)
using Testcontainers.MsSql;
#endif
#if (postgresql)
using Testcontainers.PostgreSql;
#endif
using KitApi.Infrastructure.Persistence;

namespace KitApi.IntegrationTests;

/// <summary>Un contenedor del motor del perfil para toda la colección de tests. La BD se limpia con Respawn entre tests.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Imagen fija y explícita: misma versión mayor que producción (standards/13).
#if (sqlserver)
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
#endif
#if (postgresql)
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();
#endif
    private Respawner? _respawner;
#if (useApiKey)

    /// <summary>Clave de un cliente de prueba (header X-Api-Key); en configuración solo va su hash.</summary>
    public const string TestApiKey = "test-api-key-0123456789";
#endif

    public string ConnectionString => _db.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
#if (security)
        builder.UseSetting("Jwt:Issuer", "KitApi.Tests");
        builder.UseSetting("Jwt:Audience", "KitApi.Tests");
        builder.UseSetting("Jwt:SigningKey", new string('k', 64));
        builder.UseSetting("App:ClientUrl", "http://localhost");
        builder.UseSetting("Seed:AdminEmail", TestUsers.AdminEmail);
        builder.UseSetting("Seed:AdminPassword", TestUsers.AdminPassword);
#endif
#if (analytics)
        builder.UseSetting("Analytics:HashKey", new string('h', 64));
#endif
#if (useApiKey)
        builder.UseSetting("ApiKeys:Clients:0:ClientId", "tests");
        builder.UseSetting("ApiKeys:Clients:0:Name", "Tests");
        builder.UseSetting("ApiKeys:Clients:0:KeyHash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TestApiKey))));
#endif
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server;   // fuerza el arranque: migraciones + seed

        await using var connection = await OpenConnectionAsync();
        _respawner = await CreateRespawnerAsync(connection);
    }

    private static async Task<Respawner?> CreateRespawnerAsync(DbConnection connection)
    {
        try
        {
            return await Respawner.CreateAsync(connection, new RespawnerOptions
            {
#if (sqlserver)
                DbAdapter = DbAdapter.SqlServer,
#endif
#if (postgresql)
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
#endif
                // No se borran: historial de migraciones y catálogos sembrados (roles y sus permisos).
                TablesToIgnore = ["__EFMigrationsHistory", "AspNetRoles", "AspNetRoleClaims"]
            });
        }
        catch (InvalidOperationException)
        {
            return null;   // proyecto recién creado: todavía no hay tablas que limpiar
        }
    }

    /// <summary>Deja la BD como recién creada antes de cada test.</summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is not null)
        {
            await using var connection = await OpenConnectionAsync();
            await _respawner.ResetAsync(connection);
        }
#if (security)
        await DatabaseSeeder.SeedAsync(Services);   // recrea el admin (AspNetUsers se vació)
#endif
    }

    /// <summary>Inserta datos de prueba con el DbContext real (interceptor de auditoría incluido).</summary>
    public async Task<TEntity> SeedAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
#if (sqlserver)
        DbConnection connection = new SqlConnection(ConnectionString);
#endif
#if (postgresql)
        DbConnection connection = new NpgsqlConnection(ConnectionString);
#endif
        await connection.OpenAsync();
        return connection;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
