using System.Linq.Expressions;
using FluentValidation;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;

namespace KitApi.Features.KitEntities;

// ---------- DTOs y requests ----------

public sealed record KitEntityDto(
    EntityKey Id,
    string Name,
#if (code)
    string Code,
#endif
#if (active)
    bool IsActive,
#endif
#if (hasParent)
    EntityKey KitParentId,
    string KitParentName,
#endif
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);      // token de concurrencia: el cliente lo devuelve tal cual en el Update

public sealed record CreateKitEntityRequest
{
    public string Name { get; init; } = string.Empty;
#if (code)
    public string Code { get; init; } = string.Empty;
#endif
#if (hasParent)
    public EntityKey KitParentId { get; init; }
#endif
}

public sealed record UpdateKitEntityRequest
{
    public string Name { get; init; } = string.Empty;
#if (active)
    public bool IsActive { get; init; }
#endif
#if (hasParent)
    public EntityKey KitParentId { get; init; }
#endif
    public string RowVersion { get; init; } = string.Empty;
}

/// <summary>Filtros + paginación (se enlaza desde query string).</summary>
public sealed class KitEntityFilter : PaginationParams
{
    public string? Search { get; init; }
#if (hasParent)
    public EntityKey? KitParentId { get; init; }
#endif
#if (active)
    public bool? IsActive { get; init; }
#endif
    public string? SortBy { get; init; }          // valores permitidos: los del switch de la spec
    public bool SortDescending { get; init; }
}

// ---------- Errores (nunca strings sueltos en el service) ----------

public static class KitEntityErrors
{
    public static Error NotFound(EntityKey id) => Error.NotFound("KitEntity.NotFound", $"No existe el registro {id}.");
#if (code)
    public static Error CodeAlreadyExists(string code) => Error.Conflict("KitEntity.CodeAlreadyExists", $"Ya existe un registro con código '{code}'.");
#endif
#if (hasParent)
    public static Error KitParentNotFound(EntityKey id) => Error.Validation("KitEntity.KitParentNotFound", $"El KitParent {id} no existe.");
#endif
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("KitEntity.Concurrency", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
}

// ---------- Mapeo manual (sin AutoMapper) ----------

public static class KitEntityMappings
{
    /// <summary>Proyección traducible a SQL: usar con ListAsync / PagedListAsync / FirstOrDefaultAsync(spec, Projection).</summary>
    public static readonly Expression<Func<KitEntity, KitEntityDto>> Projection = e => new KitEntityDto(
        e.Id,
        e.Name,
#if (code)
        e.Code,
#endif
#if (active)
        e.IsActive,
#endif
#if (hasParent)
        e.KitParentId,
        e.KitParent.Name,
#endif
        e.CreatedAt,
        e.UpdatedAt,
        e.RowVersion.ToString());

    public static KitEntity ToEntity(this CreateKitEntityRequest request) => new()
    {
        Name = request.Name.Trim(),
#if (code)
        Code = request.Code.Trim().ToUpperInvariant(),
#endif
#if (hasParent)
        KitParentId = request.KitParentId,
#endif
    };

    public static void ApplyTo(this UpdateKitEntityRequest request, KitEntity entity)
    {
        entity.Name = request.Name.Trim();
#if (active)
        entity.IsActive = request.IsActive;
#endif
#if (hasParent)
        entity.KitParentId = request.KitParentId;
#endif
    }
}

// ---------- Validadores: forma de la entrada (las reglas que necesitan BD van en el service) ----------

public sealed class CreateKitEntityRequestValidator : AbstractValidator<CreateKitEntityRequest>
{
    public CreateKitEntityRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
#if (code)
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
#endif
#if (hasParent)
        RuleFor(x => x.KitParentId).NotEmpty();
#endif
    }
}

public sealed class UpdateKitEntityRequestValidator : AbstractValidator<UpdateKitEntityRequest>
{
    public UpdateKitEntityRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
#if (hasParent)
        RuleFor(x => x.KitParentId).NotEmpty();
#endif
        RuleFor(x => x.RowVersion).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("Token de concurrencia inválido.");
    }
}
