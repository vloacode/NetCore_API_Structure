using System.Linq.Expressions;
using FluentValidation;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;

namespace KitApi.Features.KitEntities;

// ---------- DTOs y requests ----------

public sealed record KitEntityDto(
    int Id,
    string Name,
    string Code,
    bool IsActive,
#if (hasParent)
    int KitParentId,
    string KitParentName,
#endif
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);      // token de concurrencia: el cliente lo devuelve tal cual en el Update

#if (hasParent)
public sealed record CreateKitEntityRequest(string Name, string Code, int KitParentId);

public sealed record UpdateKitEntityRequest(string Name, bool IsActive, int KitParentId, string RowVersion);
#else
public sealed record CreateKitEntityRequest(string Name, string Code);

public sealed record UpdateKitEntityRequest(string Name, bool IsActive, string RowVersion);
#endif

/// <summary>Filtros + paginación (se enlaza desde query string).</summary>
public sealed class KitEntityFilter : PaginationParams
{
    public string? Search { get; init; }
#if (hasParent)
    public int? KitParentId { get; init; }
#endif
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }          // valores permitidos: los del switch de la spec
    public bool SortDescending { get; init; }
}

// ---------- Errores (nunca strings sueltos en el service) ----------

public static class KitEntityErrors
{
    public static Error NotFound(int id) => Error.NotFound("KitEntity.NotFound", $"No existe el registro {id}.");
    public static Error CodeAlreadyExists(string code) => Error.Conflict("KitEntity.CodeAlreadyExists", $"Ya existe un registro con código '{code}'.");
#if (hasParent)
    public static Error KitParentNotFound(int id) => Error.Validation("KitEntity.KitParentNotFound", $"El KitParent {id} no existe.");
#endif
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("KitEntity.Concurrency", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
}

// ---------- Mapeo manual (sin AutoMapper) ----------

public static class KitEntityMappings
{
    /// <summary>Proyección traducible a SQL: usar con ListAsync / PagedListAsync / FirstOrDefaultAsync(spec, Projection).</summary>
    public static readonly Expression<Func<KitEntity, KitEntityDto>> Projection = e => new KitEntityDto(
        e.Id, e.Name, e.Code, e.IsActive,
#if (hasParent)
        e.KitParentId, e.KitParent.Name,
#endif
        e.CreatedAt, e.UpdatedAt,
        e.RowVersion.ToString());

    public static KitEntity ToEntity(this CreateKitEntityRequest request) => new()
    {
        Name = request.Name.Trim(),
#if (hasParent)
        Code = request.Code.Trim().ToUpperInvariant(),
        KitParentId = request.KitParentId
#else
        Code = request.Code.Trim().ToUpperInvariant()
#endif
    };

    public static void ApplyTo(this UpdateKitEntityRequest request, KitEntity entity)
    {
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
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
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
#if (hasParent)
        RuleFor(x => x.KitParentId).GreaterThan(0);
#endif
    }
}

public sealed class UpdateKitEntityRequestValidator : AbstractValidator<UpdateKitEntityRequest>
{
    public UpdateKitEntityRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
#if (hasParent)
        RuleFor(x => x.KitParentId).GreaterThan(0);
#endif
        RuleFor(x => x.RowVersion).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("Token de concurrencia inválido.");
    }
}
