# Plantillas de entidades de negocio

> **Aplica a:** Todos los perfiles (con variante sin seguridad)  
> **Propósito:** Molde para crear cada entidad de negocio: entidad, configuración, contratos, specs, validadores, service y controller.  
> Índice general: `standards/00-INDEX.md`

## Entidades de negocio (plantillas)

> **IMPORTANTE para la IA:** esta sección no define ninguna entidad real. Es el **molde** para cada entidad que el usuario pida.
> - Reemplazar `{Entity}`, `{Entities}`, `{entity}`, `{entities}` y `{Parent}` por los nombres reales.
> - `Name` y `Code` son campos **representativos**: sustituirlos por las propiedades reales del dominio.
> - Crear **solo** las entidades acordadas con el usuario y documentadas en `docs/05-domain-model.md` y `docs/modules/`. No crear entidades de ejemplo.
> - Líneas marcadas `[SEC]` solo aplican con seguridad (ver `standards/01`, "Perfiles").
> - Cada entidad vive en su carpeta `Application/Features/{Entities}/`.

### Entidad — `Domain/Entities/{Entity}.cs`
```csharp
using {Project}.Domain.Common;

namespace {Project}.Domain.Entities;

public class {Entity} : BaseEntity
{
    // Propiedades reales del dominio. Name/Code son representativas.
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;      // si existe un identificador de negocio único
    public bool IsActive { get; set; } = true;           // bool que arranca en true: inicializador C#, no HasDefaultValue

    // Relación N:1 (si aplica)
    public int {Parent}Id { get; set; }
    public {Parent} {Parent} { get; set; } = null!;

    // Relación 1:N (si aplica)
    // public ICollection<{Child}> {Children} { get; } = new List<{Child}>();
}
```

### Configuración EF — `Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Domain.Entities;

namespace {Project}.Infrastructure.Persistence.Configurations;

public sealed class {Entity}Configuration : BaseEntityConfiguration<{Entity}>
{
    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        base.Configure(builder);
        builder.ToTable("{Entities}");

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        // decimales: builder.Property(e => e.{Monto}).HasPrecision(18, 2);

        // Único solo entre los no eliminados (soft delete).
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.{Parent})
               .WithMany(/* p => p.{Entities} */)
               .HasForeignKey(e => e.{Parent}Id)
               .OnDelete(DeleteBehavior.Restrict);   // con soft delete, nunca Cascade en negocio
    }
}
```
Y en `AppDbContext`: `public DbSet<{Entity}> {Entities} => Set<{Entity}>();`

### Contratos, errores y mapeo — `Application/Features/{Entities}/{Entity}Contracts.cs`
```csharp
using System.Linq.Expressions;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

// ---------- DTOs ----------

public sealed record {Entity}Dto(
    int Id,
    string Name,
    string Code,
    bool IsActive,
    int {Parent}Id,
    string {Parent}Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);      // Base64: el cliente la devuelve en el Update (concurrencia)

public sealed record Create{Entity}Request(string Name, string Code, int {Parent}Id);

public sealed record Update{Entity}Request(string Name, bool IsActive, int {Parent}Id, string RowVersion);

/// <summary>Filtros + paginación (se enlaza desde query string).</summary>
public sealed class {Entity}Filter : PaginationParams
{
    public string? Search { get; init; }
    public int? {Parent}Id { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }          // valores permitidos definidos en la spec
    public bool SortDescending { get; init; }
}

// ---------- Errores (nunca strings sueltos en el service) ----------

public static class {Entity}Errors
{
    public static Error NotFound(int id) => Error.NotFound("{Entity}.NotFound", $"No existe el registro {id}.");
    public static Error CodeAlreadyExists(string code) => Error.Conflict("{Entity}.CodeAlreadyExists", $"Ya existe un registro con código '{code}'.");
    public static Error {Parent}NotFound(int id) => Error.Validation("{Entity}.{Parent}NotFound", $"El {Parent} {id} no existe.");
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("{Entity}.Concurrency", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
}

// ---------- Mapeo manual (sin AutoMapper) ----------

public static class {Entity}Mappings
{
    /// <summary>Proyección traducible a SQL: usar con repo.ListAsync / PagedListAsync / FirstOrDefaultAsync(spec, Projection).</summary>
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> Projection = e => new {Entity}Dto(
        e.Id, e.Name, e.Code, e.IsActive,
        e.{Parent}Id, e.{Parent}.Name,
        e.CreatedAt, e.UpdatedAt,
        Convert.ToBase64String(e.RowVersion));

    public static {Entity} ToEntity(this Create{Entity}Request request) => new()
    {
        Name = request.Name.Trim(),
        Code = request.Code.Trim().ToUpperInvariant(),
        {Parent}Id = request.{Parent}Id
    };

    public static void ApplyTo(this Update{Entity}Request request, {Entity} entity)
    {
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.{Parent}Id = request.{Parent}Id;
    }
}
```

### Especificaciones — `Application/Features/{Entities}/{Entity}Specifications.cs`
```csharp
using {Project}.Application.Common.Specifications;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

public sealed class {Entity}ByIdSpec : Specification<{Entity}>
{
    public {Entity}ByIdSpec(int id, bool tracking = false) : base(e => e.Id == id)
    {
        Include(e => e.{Parent});
        if (tracking) EnableTracking();
    }
}

/// <summary>Búsqueda paginada con filtros dinámicos y orden estable.</summary>
public sealed class {Entities}ByFilterSpec : Specification<{Entity}>
{
    public {Entities}ByFilterSpec({Entity}Filter filter)
    {
        var search = filter.Search?.Trim();

        Where(PredicateBuilder.True<{Entity}>()
            .AndIf(!string.IsNullOrEmpty(search), e => e.Name.Contains(search!) || e.Code.Contains(search!))
            .AndIf(filter.{Parent}Id.HasValue, e => e.{Parent}Id == filter.{Parent}Id)
            .AndIf(filter.IsActive.HasValue, e => e.IsActive == filter.IsActive));

        // Lista blanca de campos ordenables (nunca ordenar por un string arbitrario del cliente).
        switch (filter.SortBy?.ToLowerInvariant())
        {
            case "createdat":
                if (filter.SortDescending) OrderByDescending(e => e.CreatedAt); else OrderBy(e => e.CreatedAt);
                break;
            default:
                if (filter.SortDescending) OrderByDescending(e => e.Name); else OrderBy(e => e.Name);
                break;
        }

        OrderBy(e => e.Id);   // desempate: paginación determinista
        ApplyPaging(filter);
    }
}
```

### Validadores — `Application/Features/{Entities}/{Entity}Validators.cs`
```csharp
using FluentValidation;

namespace {Project}.Application.Features.{Entities};

public sealed class Create{Entity}RequestValidator : AbstractValidator<Create{Entity}Request>
{
    public Create{Entity}RequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.{Parent}Id).GreaterThan(0);
    }
}

public sealed class Update{Entity}RequestValidator : AbstractValidator<Update{Entity}Request>
{
    public Update{Entity}RequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.{Parent}Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
```
Regla: el **validador** revisa forma (vacíos, longitudes, rangos). El **service** revisa reglas que necesitan BD (duplicados, existencia de FKs, estados).

### Service — `Application/Features/{Entities}/{Entity}Service.cs`
```csharp
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

public interface I{Entity}Service
{
    Task<Result<{Entity}Dto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<{Entity}Dto>>> GetPagedAsync({Entity}Filter filter, CancellationToken ct);
    Task<Result<{Entity}Dto>> CreateAsync(Create{Entity}Request request, CancellationToken ct);
    Task<Result<{Entity}Dto>> UpdateAsync(int id, Update{Entity}Request request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}

public sealed class {Entity}Service(IUnitOfWork uow) : I{Entity}Service
{
    private readonly IRepository<{Entity}> _repo = uow.Repository<{Entity}>();
    private readonly IRepository<{Parent}> _parents = uow.Repository<{Parent}>();

    public async Task<Result<{Entity}Dto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await _repo.FirstOrDefaultAsync(new {Entity}ByIdSpec(id), {Entity}Mappings.Projection, ct);
        return dto is null ? {Entity}Errors.NotFound(id) : dto;
    }

    public async Task<Result<PagedResult<{Entity}Dto>>> GetPagedAsync({Entity}Filter filter, CancellationToken ct)
        => await _repo.PagedListAsync(new {Entities}ByFilterSpec(filter), {Entity}Mappings.Projection, filter, ct);

    public async Task<Result<{Entity}Dto>> CreateAsync(Create{Entity}Request request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repo.AnyAsync(e => e.Code == code, ct))
            return {Entity}Errors.CodeAlreadyExists(code);

        if (!await _parents.AnyAsync(p => p.Id == request.{Parent}Id, ct))
            return {Entity}Errors.{Parent}NotFound(request.{Parent}Id);

        var entity = request.ToEntity();
        _repo.Add(entity);
        await uow.SaveChangesAsync(ct);          // la auditoría la pone el interceptor

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<Result<{Entity}Dto>> UpdateAsync(int id, Update{Entity}Request request, CancellationToken ct)
    {
        var entity = await _repo.FirstOrDefaultAsync(new {Entity}ByIdSpec(id, tracking: true), ct);
        if (entity is null)
            return {Entity}Errors.NotFound(id);

        // Concurrencia optimista: el cliente envía la RowVersion que leyó.
        if (!entity.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(request.RowVersion)))
            return {Entity}Errors.ConcurrencyConflict;

        if (entity.{Parent}Id != request.{Parent}Id && !await _parents.AnyAsync(p => p.Id == request.{Parent}Id, ct))
            return {Entity}Errors.{Parent}NotFound(request.{Parent}Id);

        request.ApplyTo(entity);                 // entidad trackeada: no hace falta llamar Update()
        await uow.SaveChangesAsync(ct);          // DbUpdateConcurrencyException → 409 en el handler global

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
            return {Entity}Errors.NotFound(id);

        _repo.Remove(entity);                    // soft delete vía interceptor
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

### Controller — `Api/Controllers/{Entities}Controller.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.{Entities};

namespace {Project}.Api.Controllers;

public sealed class {Entities}Controller(I{Entity}Service service) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.{Entities}.Read)]
    [ProducesResponseType<PagedResult<{Entity}Dto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] {Entity}Filter filter, CancellationToken ct)
        => HandleResult(await service.GetPagedAsync(filter, ct));

    [HttpGet("{id:int}"), HasPermission(Permissions.{Entities}.Read)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => HandleResult(await service.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.{Entities}.Write)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Create{Entity}Request request, CancellationToken ct)
        => HandleCreated(await service.CreateAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

    [HttpPut("{id:int}"), HasPermission(Permissions.{Entities}.Write)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, Update{Entity}Request request, CancellationToken ct)
        => HandleResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}"), HasPermission(Permissions.{Entities}.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => HandleResult(await service.DeleteAsync(id, ct));
}
```

**Variante sin seguridad (API pública):** el mismo controller sin `HasPermission` y sin los `using` de `Authorization` y `Security`:
```csharp
[HttpGet]
[ProducesResponseType<PagedResult<{Entity}Dto>>(StatusCodes.Status200OK)]
public async Task<IActionResult> GetPaged([FromQuery] {Entity}Filter filter, CancellationToken ct)
    => HandleResult(await service.GetPagedAsync(filter, ct));
// ... resto igual, solo se quita ", HasPermission(...)" de cada atributo.
```
En una API pública, decidir con el usuario si las operaciones de escritura (POST/PUT/DELETE) deben existir o protegerse con una API key (`ai/suggestions-catalog.md`).
