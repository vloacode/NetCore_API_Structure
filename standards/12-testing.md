# Testing

> **Aplica a:** Todos los perfiles (casos de Auth solo con seguridad)  
> **Propósito:** Estrategia de pruebas, proyectos, paquetes, fixtures y casos mínimos.  
> Índice general: `standards/00-INDEX.md`

## Estrategia

| Tipo | Qué prueba | Base de datos | Proyecto |
|---|---|---|---|
| **Unit** | Validadores, `Result`, `PredicateBuilder`, mapeos, lógica pura de dominio | Ninguna | `tests/{Project}.UnitTests` |
| **Integration (services)** | Services con `UnitOfWork` real: reglas, duplicados, concurrencia, soft delete, transacciones | **SQL Server real** (Testcontainers) | `tests/{Project}.IntegrationTests` |
| **Integration (API)** | Endpoints completos: rutas, permisos, validación, códigos HTTP, ProblemDetails | SQL Server real (Testcontainers) | `tests/{Project}.IntegrationTests` |

> ⚠️ **No usar SQLite ni el proveedor InMemory para probar services.** El modelo usa funciones de SQL Server (`SYSUTCDATETIME()`, `rowversion`, índices filtrados), y esos proveedores no se comportan igual: los tests pasarían o fallarían por razones falsas.

## Crear los proyectos
```bash
dotnet new xunit -n {Project}.UnitTests -o tests/{Project}.UnitTests
dotnet new xunit -n {Project}.IntegrationTests -o tests/{Project}.IntegrationTests
dotnet add tests/{Project}.UnitTests reference src/{Project}.Api
dotnet add tests/{Project}.IntegrationTests reference src/{Project}.Api
dotnet add tests/{Project}.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/{Project}.IntegrationTests package Testcontainers.MsSql
dotnet add tests/{Project}.IntegrationTests package Respawn
dotnet add tests/{Project}.UnitTests package Shouldly
dotnet add tests/{Project}.IntegrationTests package Shouldly
dotnet add tests/{Project}.UnitTests package Microsoft.Extensions.TimeProvider.Testing
```
- Aserciones: **Shouldly** o las de xUnit. Evitar FluentAssertions 8+, que cambió a licencia comercial.
- Dobles de prueba para dependencias externas (email, HTTP): **NSubstitute**. No se "mockean" `IRepository` ni `IUnitOfWork`; se usa la BD real.
- Requiere **Docker** en la máquina y en CI para Testcontainers.

## Fixture de API — `tests/{Project}.IntegrationTests/ApiFactory.cs`
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Respawn;
using Testcontainers.MsSql;

namespace {Project}.IntegrationTests;

/// <summary>Un SQL Server en contenedor para toda la colección de tests. La BD se limpia con Respawn entre tests.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder().Build();
    private Respawner _respawner = null!;

    public string ConnectionString => _db.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", new string('k', 64));          // [SEC]
        builder.UseSetting("Seed:AdminEmail", TestUsers.AdminEmail);        // [SEC]
        builder.UseSetting("Seed:AdminPassword", TestUsers.AdminPassword);  // [SEC]
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server;   // fuerza el arranque: migraciones + seed
        _respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory", "AspNetRoles", "AspNetRoleClaims"]   // conserva roles del seed
        });
    }

    public Task ResetDatabaseAsync() => _respawner.ResetAsync(ConnectionString);

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
```
> Escrito para **xUnit v3** (`IAsyncLifetime` con `ValueTask`). En xUnit v2 los métodos devuelven `Task`. Si la API de Testcontainers o Respawn cambió, ajustar a la versión instalada.
> `[SEC]`: si Respawn borra también `AspNetUsers`, recrear el admin con el seeder en `ResetDatabaseAsync`.

## Helper de autenticación `[SEC]`
```csharp
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

## Ejemplo de test de API
```csharp
[Collection(nameof(ApiCollection))]
public sealed class {Entities}ApiTests(ApiFactory factory) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => new(factory.ResetDatabaseAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Create_WithDuplicateCode_Returns409WithCode()
    {
        var client = await factory.CreateAdminClientAsync();
        var request = new { name = "Uno", code = "A-1", {parent}Id = await SeedParentAsync() };
        (await client.PostAsJsonAsync("/api/{entities}", request)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/{entities}", request);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("{Entity}.CodeAlreadyExists");
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
dotnet test --filter "FullyQualifiedName~{Entities}"
dotnet test --collect:"XPlat Code Coverage"
```
