#if (postgresql)
using Microsoft.EntityFrameworkCore;
#endif
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;
using KitApi.Application.Common.Specifications;
#if (hasParent)
using KitApi.Features.ParentFeature;
#endif

namespace KitApi.Features.KitEntities;

public interface IKitEntityService
{
    Task<Result<KitEntityDto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<KitEntityDto>>> GetPagedAsync(KitEntityFilter filter, CancellationToken ct);
    Task<Result<KitEntityDto>> CreateAsync(CreateKitEntityRequest request, CancellationToken ct);
    Task<Result<KitEntityDto>> UpdateAsync(int id, UpdateKitEntityRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Se registra solo (AddApplication busca I{Nombre}Service en Features/).</summary>
public sealed class KitEntityService(IUnitOfWork uow) : IKitEntityService
{
    private readonly IRepository<KitEntity> _repo = uow.Repository<KitEntity>();
#if (hasParent)
    private readonly IRepository<KitParent> _parents = uow.Repository<KitParent>();
#endif

    public async Task<Result<KitEntityDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await _repo.FirstOrDefaultAsync(new KitEntityByIdSpec(id), KitEntityMappings.Projection, ct);
        return dto is null ? KitEntityErrors.NotFound(id) : dto;
    }

    public async Task<Result<PagedResult<KitEntityDto>>> GetPagedAsync(KitEntityFilter filter, CancellationToken ct)
        => await _repo.PagedListAsync(new KitEntitiesByFilterSpec(filter), KitEntityMappings.Projection, filter, ct);

    public async Task<Result<KitEntityDto>> CreateAsync(CreateKitEntityRequest request, CancellationToken ct)
    {
#if (code)
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.AnyAsync(e => e.Code == code, ct))
            return KitEntityErrors.CodeAlreadyExists(code);

#endif
#if (hasParent)
        if (!await _parents.AnyAsync(p => p.Id == request.KitParentId, ct))
            return KitEntityErrors.KitParentNotFound(request.KitParentId);

#endif
        var entity = request.ToEntity();
        _repo.Add(entity);
        await uow.SaveChangesAsync(ct);          // la auditoría la pone el interceptor

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<Result<KitEntityDto>> UpdateAsync(int id, UpdateKitEntityRequest request, CancellationToken ct)
    {
        var entity = await _repo.FirstOrDefaultAsync(new KitEntityByIdSpec(id, tracking: true), ct);
        if (entity is null)
            return KitEntityErrors.NotFound(id);

        // Concurrencia optimista: el cliente envía la RowVersion que leyó.
        if (!Guid.TryParse(request.RowVersion, out var rowVersion) || rowVersion != entity.RowVersion)
            return KitEntityErrors.ConcurrencyConflict;
#if (hasParent)

        if (entity.KitParentId != request.KitParentId && !await _parents.AnyAsync(p => p.Id == request.KitParentId, ct))
            return KitEntityErrors.KitParentNotFound(request.KitParentId);
#endif

        request.ApplyTo(entity);                 // entidad trackeada: no hace falta llamar Update()
        await uow.SaveChangesAsync(ct);          // DbUpdateConcurrencyException → 409 en el handler global

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
            return KitEntityErrors.NotFound(id);

        _repo.Remove(entity);                    // soft delete vía interceptor
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Especificaciones: una clase por consulta, con nombre de negocio ----------

public sealed class KitEntityByIdSpec : Specification<KitEntity>
{
    public KitEntityByIdSpec(int id, bool tracking = false) : base(e => e.Id == id)
    {
#if (hasParent)
        Include(e => e.KitParent);
#endif
        if (tracking) EnableTracking();
    }
}

/// <summary>Búsqueda paginada con filtros dinámicos y orden estable.</summary>
public sealed class KitEntitiesByFilterSpec : Specification<KitEntity>
{
    public KitEntitiesByFilterSpec(KitEntityFilter filter)
    {
        var search = filter.Search?.Trim();

        // Búsqueda de texto: Contains en SQL Server (collation sin mayúsculas), ILike en PostgreSQL.
        var predicate = PredicateBuilder.True<KitEntity>()
#if (sqlserver && code)
            .AndIf(!string.IsNullOrEmpty(search), e => e.Name.Contains(search!) || e.Code.Contains(search!));
#elif (sqlserver)
            .AndIf(!string.IsNullOrEmpty(search), e => e.Name.Contains(search!));
#elif (code)
            .AndIf(!string.IsNullOrEmpty(search), e => EF.Functions.ILike(e.Name, $"%{search}%") || EF.Functions.ILike(e.Code, $"%{search}%"));
#else
            .AndIf(!string.IsNullOrEmpty(search), e => EF.Functions.ILike(e.Name, $"%{search}%"));
#endif
#if (hasParent)
        predicate = predicate.AndIf(filter.KitParentId.HasValue, e => e.KitParentId == filter.KitParentId);
#endif
#if (active)
        predicate = predicate.AndIf(filter.IsActive.HasValue, e => e.IsActive == filter.IsActive);
#endif
        Where(predicate);

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
