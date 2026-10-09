# Testing

> **Aplica a:** Todos los perfiles (casos de Auth solo con seguridad)  
> **Propósito:** Estrategia de pruebas, proyectos, paquetes, fixtures y casos mínimos.  
> Índice general: `standards/00-INDEX.md`

## Estrategia

| Tipo | Qué prueba | Base de datos | Proyecto |
|---|---|---|---|
| **Unit** | Validadores, `Result`, `PredicateBuilder`, mapeos, lógica pura de dominio | Ninguna | `tests/{Project}.UnitTests` |
| **Integration (services)** | Services con `UnitOfWork` real: reglas, duplicados, concurrencia, soft delete, transacciones | **El motor real del perfil** (Testcontainers: SQL Server o PostgreSQL) | `tests/{Project}.IntegrationTests` |
| **Integration (API)** | Endpoints completos: rutas, permisos, validación, códigos HTTP, ProblemDetails | El motor real del perfil (Testcontainers) | `tests/{Project}.IntegrationTests` |

> ⚠️ **No usar SQLite ni el proveedor InMemory para probar services.** El modelo usa índices filtrados, collations y tipos propios de cada motor, y esos proveedores no se comportan igual: los tests pasarían o fallarían por razones falsas.

## Crear los proyectos
```bash
dotnet new xunit -n {Project}.UnitTests -o tests/{Project}.UnitTests
dotnet new xunit -n {Project}.IntegrationTests -o tests/{Project}.IntegrationTests
# La plantilla de .NET 10 trae xUnit v2: cambiar a xUnit v3 en ambos proyectos
dotnet remove tests/{Project}.UnitTests package xunit
dotnet remove tests/{Project}.IntegrationTests package xunit
dotnet add tests/{Project}.UnitTests package xunit.v3
dotnet add tests/{Project}.IntegrationTests package xunit.v3
dotnet sln add tests/{Project}.UnitTests tests/{Project}.IntegrationTests
dotnet add tests/{Project}.UnitTests reference src/{Project}.Api
dotnet add tests/{Project}.IntegrationTests reference src/{Project}.Api
dotnet add tests/{Project}.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/{Project}.IntegrationTests package Microsoft.Testing.Extensions.CodeCoverage
dotnet add tests/{Project}.IntegrationTests package Testcontainers.MsSql        # [MSSQL]
dotnet add tests/{Project}.IntegrationTests package Testcontainers.PostgreSql   # [PGSQL]
dotnet add tests/{Project}.IntegrationTests package Respawn
dotnet add tests/{Project}.UnitTests package Shouldly
dotnet add tests/{Project}.IntegrationTests package Shouldly
dotnet add tests/{Project}.UnitTests package Microsoft.Extensions.TimeProvider.Testing
```
- Aserciones: **Shouldly** o las de xUnit. Evitar FluentAssertions 8+, que cambió a licencia comercial.
- Dobles de prueba para dependencias externas (email, HTTP): **NSubstitute**. No se "mockean" `IRepository` ni `IUnitOfWork`; se usa la BD real.
- Requiere **Docker** en la máquina y en CI para Testcontainers.
- En cada `.csproj` de test: `<OutputType>Exe</OutputType>` (xUnit v3 genera un ejecutable) y quitar `coverlet.collector` (la cobertura la da `Microsoft.Testing.Extensions.CodeCoverage`).
- `global.json` con `"test": { "runner": "Microsoft.Testing.Platform" }` y `tests/.editorconfig` con las reglas de tests (`standards/14`).

## Fixture de API — `tests/{Project}.IntegrationTests/ApiFactory.cs`
```csharp
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;      // [MSSQL]
using Microsoft.Extensions.DependencyInjection;
using Npgsql;                        // [PGSQL]
using Respawn;
using Testcontainers.MsSql;          // [MSSQL]
using Testcontainers.PostgreSql;     // [PGSQL]
using {Project}.Infrastructure.Persistence;

namespace {Project}.IntegrationTests;

/// <summary>Un contenedor del motor del perfil para toda la colección de tests. La BD se limpia con Respawn entre tests.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Imagen fija y explícita: misma versión mayor que producción (standards/13).
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();   // [MSSQL]
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();                         // [PGSQL]
    private Respawner _respawner = null!;

    public string ConnectionString => _db.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Jwt:Issuer", "{Project}.Tests");                // [SEC]
        builder.UseSetting("Jwt:Audience", "{Project}.Tests");              // [SEC]
        builder.UseSetting("Jwt:SigningKey", new string('k', 64));          // [SEC]
        builder.UseSetting("App:ClientUrl", "http://localhost");            // [SEC]
        builder.UseSetting("Seed:AdminEmail", TestUsers.AdminEmail);        // [SEC]
        builder.UseSetting("Seed:AdminPassword", TestUsers.AdminPassword);  // [SEC]
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server;   // fuerza el arranque: migraciones + seed

        await using var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,   // [MSSQL]
            DbAdapter = DbAdapter.Postgres,    // [PGSQL]
            SchemasToInclude = ["public"],     // [PGSQL]
            // No se borran: historial de migraciones y catálogos sembrados (roles y sus permisos).
            TablesToIgnore = ["__EFMigrationsHistory", "AspNetRoles", "AspNetRoleClaims"]
        });
    }

    /// <summary>Deja la BD como recién creada antes de cada test.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
        await DatabaseSeeder.SeedAsync(Services);   // [SEC] recrea el admin (AspNetUsers se vació)
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
        DbConnection connection = new SqlConnection(ConnectionString);      // [MSSQL]
        DbConnection connection = new NpgsqlConnection(ConnectionString);   // [PGSQL]
        await connection.OpenAsync();
        return connection;
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
```
> Escrito para **xUnit v3** (`IAsyncLifetime` con `ValueTask`). Si la API de Testcontainers o Respawn cambia en una versión nueva, ajustar a la versión instalada.

## Helper de autenticación `[SEC]` — `tests/{Project}.IntegrationTests/TestUsers.cs`
```csharp
using System.Net.Http.Json;
using {Project}.Application.Features.Auth;

namespace {Project}.IntegrationTests;

public static class TestUsers
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Test#Pass12345";

    public static async Task<HttpClient> CreateAdminClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = AdminEmail, password = AdminPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login!.Tokens!.AccessToken);
        return client;
    }
}
```
Para probar permisos concretos, crear un usuario con un rol de prueba y loguearse con él.

## Ejemplo de tests de API — `tests/{Project}.IntegrationTests/{Entities}ApiTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using {Project}.Domain.Entities;

namespace {Project}.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class {Entities}ApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetDatabaseAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409WithCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await factory.CreateAdminClientAsync();   // [SEC]
        var client = factory.CreateClient();                   // [PUB]
        var parent = await factory.SeedAsync(new {Parent} { Name = "Padre" });
        var request = new { name = "Uno", code = "A-1", {parent}Id = parent.Id };
        (await client.PostAsJsonAsync("/api/{entities}", request, ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/{entities}", request, ct);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("code").GetString().ShouldBe("{Entity}.CodeAlreadyExists");
    }

    [Fact]
    public async Task GetById_WhenMissing_Returns404WithCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await factory.CreateAdminClientAsync();   // [SEC]
        var client = factory.CreateClient();                   // [PUB]

        var response = await client.GetAsync("/api/{entities}/999999", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        problem.GetProperty("code").GetString().ShouldBe("{Entity}.NotFound");
    }

    // [SEC]
    [Fact]
    public async Task GetPaged_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/{entities}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

## Convenciones
- Nombre: `Metodo_Escenario_ResultadoEsperado` (`Create_WithDuplicateCode_Returns409WithCode`).
- Estructura Arrange / Act / Assert, separada por líneas en blanco.
- Un comportamiento por test. Sin lógica (`if`, bucles) dentro del test.
- Datos de prueba creados en el propio test o con helpers `Seed*Async`. Nunca depender del orden de ejecución.
- Fechas controladas con `FakeTimeProvider` cuando la lógica depende del tiempo.

## Casos mínimos por entidad de negocio
- [ ] Crear válido → 201 con `Location` y DTO.
- [ ] Crear inválido → 400 con `code = Validation.Failed` y `errors` por campo.
- [ ] Duplicado → 409 con su código.
- [ ] FK inexistente → 400.
- [ ] Obtener inexistente → 404.
- [ ] Listado paginado: `totalCount` correcto, orden estable y filtros.
- [ ] Actualizar con `RowVersion` vieja → 409.
- [ ] Eliminar → 204; después, `GET` → 404 (soft delete) y la fila sigue en BD con `IsDeleted = 1`.
- [ ] `[SEC]` Sin token → 401; sin permiso → 403; con permiso → OK.

## Casos mínimos de Auth `[SEC]`
- Login correcto e incorrecto, y lockout al 5.º intento.
- Email sin confirmar → 403.
- Refresh con rotación; reuso de un token rotado → 401 y sesión revocada.
- 2FA: setup, enable, login en dos pasos y login con código de recuperación.
- Endpoint con permiso: 403 sin el permiso, 200 con el permiso y 200 como Admin.
- Usuario desactivado o bloqueado: no puede hacer login ni refresh.

## Comandos
```bash
dotnet test
dotnet test --filter-class "*{Entities}ApiTests"
dotnet test --filter-method "*Returns401"
dotnet test --coverage --coverage-output-format cobertura
```
