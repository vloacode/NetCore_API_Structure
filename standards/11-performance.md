# Rendimiento

> **Aplica a:** Todos los perfiles  
> **Propósito:** Reglas para que la API sea rápida por defecto: consultas EF Core, índices, caché, compresión y medición.  
> Índice general: `standards/00-INDEX.md`

Los objetivos concretos (latencia p95, usuarios concurrentes, volumen de datos) están en `docs/04-non-functional-requirements.md`.

## Consultas EF Core (lo que más impacta)
1. **Proyectar siempre a DTO** con `Select` (`{Entity}Mappings.Projection`). Solo se leen las columnas necesarias y no hay tracking.
2. **`AsNoTracking` por defecto**: lo aplican las specs. Activar tracking solo para modificar (`EnableTracking()`).
3. **Paginar toda lista** que pueda crecer. Nunca `ListAsync` sin límite sobre tablas de negocio.
4. **Evitar N+1**: nada de consultas dentro de un `foreach`. Incluir datos relacionados en la proyección (join) o con `Include`.
5. **`AsSplitQuery()`** cuando una spec incluye dos o más colecciones, para evitar la explosión cartesiana.
6. **Operaciones masivas** con `ExecuteUpdateAsync` / `ExecuteDeleteAsync`, sin cargar entidades.
7. **`AnyAsync`** para comprobar existencia. Nunca `CountAsync() > 0` ni cargar la entidad.
8. Filtros sobre columnas indexadas. Evitar funciones sobre la columna en el `WHERE` (`x.Name.ToLower() == ...`), porque anulan el índice. SQL Server compara sin distinguir mayúsculas con la collation por defecto. **PostgreSQL distingue mayúsculas**: las búsquedas de texto usan `EF.Functions.ILike` (variante `[PGSQL]` de las plantillas) y, en tablas grandes, un índice GIN con `pg_trgm` para que `ILIKE '%texto%'` no recorra toda la tabla.
9. **`TagWith("{Entities}ByFilterSpec")`** en consultas complejas para identificarlas en los logs, en Query Store (SQL Server) o en `pg_stat_statements` (PostgreSQL).

## Índices
- EF Core crea índices en las FK automáticamente.
- Crear índices en las columnas usadas para **filtrar y ordenar** en las specs de búsqueda.
- Índices únicos **filtrados** por soft delete: `HasFilter(SqlDialect.NotDeleted)` (`standards/02`, funciona en ambos motores).
- Índices compuestos para filtros combinados frecuentes (`{Parent}Id + IsActive`).
- Revisar el SQL generado de cada migración (`ai/workflows/database-change.md`).

## No usar DbContext pooling
`AddDbContextPool` no es compatible con el `AuditableEntityInterceptor`, que depende de servicios *scoped* (`ICurrentUserService`). Se usa `AddDbContext` normal; el costo es mínimo.

## Caché
**Qué cachear:** catálogos que cambian poco (tipos, estados, configuraciones) y lecturas muy frecuentes y costosas.
**Qué no cachear:** datos por usuario con permisos, datos que cambian a cada rato y respuestas con información sensible.

### HybridCache (preferido, en services)
```csharp
// dotnet add package Microsoft.Extensions.Caching.Hybrid
services.AddHybridCache();   // memoria; con Redis agrega un segundo nivel distribuido automáticamente

public sealed class {Entity}CatalogService(IUnitOfWork uow, HybridCache cache)
{
    private const string AllKey = "{entities}:all";
    private readonly IRepository<{Entity}> _repo = uow.Repository<{Entity}>();

    public async Task<Result<IReadOnlyList<{Entity}Dto>>> GetAllAsync(CancellationToken ct)
    {
        var items = await cache.GetOrCreateAsync(AllKey,
            async token => await _repo.ListAsync(new All{Entities}Spec(), {Entity}Mappings.Projection, token),
            cancellationToken: ct);
        return Result.Success(items);
    }

    // Invalidar al crear, editar o eliminar:
    private ValueTask InvalidateAsync(CancellationToken ct) => cache.RemoveAsync(AllKey, ct);
}
```
Claves con formato `{entities}:{criterio}`. La invalidación va **después** de un `SaveChanges` exitoso.
Referencia: https://learn.microsoft.com/aspnet/core/performance/caching/hybrid

### Output caching (respuestas GET completas)
```csharp
services.AddOutputCache();   // Program.cs: app.UseOutputCache(); después de UseCors
[HttpGet, OutputCache(Duration = 60)]
```
Por defecto **no** se cachean peticiones con `Authorization`; es seguro para endpoints públicos. Referencia: https://learn.microsoft.com/aspnet/core/performance/caching/output

## Compresión
Solo si el proxy o el servidor (IIS, App Service, Nginx) no comprime ya:
```csharp
services.AddResponseCompression(o => o.EnableForHttps = true);   // Program.cs: app.UseResponseCompression(); al inicio
```

## Llamadas a terceros
- Siempre con `IHttpClientFactory` y clientes tipados. Nunca `new HttpClient()` por llamada.
- Resiliencia estándar con timeout, reintentos y circuit breaker:
```csharp
// dotnet add package Microsoft.Extensions.Http.Resilience
services.AddHttpClient<I{External}Client, {External}Client>(c => c.BaseAddress = new Uri(configuration["{External}:BaseUrl"]!))
        .AddStandardResilienceHandler();
```

## Código async
- `async`/`await` de punta a punta. **Nunca** `.Result`, `.Wait()` ni `GetAwaiter().GetResult()`.
- Propagar `CancellationToken` hasta EF y `HttpClient`, para que una petición cancelada libere la base de datos.
- Respuestas grandes (exportaciones) con streaming, no cargando todo en memoria.

## Medir antes de optimizar
| Herramienta | Para qué |
|---|---|
| Logs de EF Core (`Microsoft.EntityFrameworkCore.Database.Command` en `Information`, solo en desarrollo) | Ver el SQL y la duración de cada consulta |
| SQL Server: Query Store y plan de ejecución · PostgreSQL: `pg_stat_statements` y `EXPLAIN (ANALYZE, BUFFERS)` | Consultas lentas e índices faltantes en producción |
| `dotnet-counters`, `dotnet-trace` | CPU, GC, threads, peticiones por segundo |
| OpenTelemetry (`standards/10`) | Latencia por endpoint y por dependencia |
| BenchmarkDotNet | Microbenchmarks de código crítico |
| k6 o NBomber | Pruebas de carga contra los objetivos de `docs/04` |

## Checklist de rendimiento por endpoint
- [ ] Proyección a DTO; sin entidades completas ni tracking innecesario.
- [ ] Lista paginada con orden estable e índice que soporte el filtro y el orden.
- [ ] Sin N+1; `AsSplitQuery` si hay varias colecciones.
- [ ] `CancellationToken` propagado.
- [ ] Caché evaluada si es un catálogo o una lectura muy frecuente.
