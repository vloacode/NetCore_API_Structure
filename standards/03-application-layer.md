# Capa Application: Result, paginación y especificaciones

> **Aplica a:** Todos los perfiles  
> **Propósito:** Cómo usar `Result`/`Error`, `PaginationParams`/`PagedResult`, `PredicateBuilder` y `Specification<T>`.  
> **Código:** [`Application/Common/`](../templates/api/src/KitApi.Api/Application/Common/)

## `Result` y `Error`
- `Result` / `Result<T>` con `Error(Code, Message, ErrorType)`. `ErrorType`: `Failure`, `Validation`, `NotFound`, `Conflict`, `Unauthorized`, `Forbidden`.
- Conversiones implícitas: en un service se escribe `return dto;` o `return {Entity}Errors.NotFound(id);`.
- Los errores se declaran en `{Entity}Errors` con código `{Entity}.{Motivo}` (`standards/08`). Nunca strings sueltos en el service.
- Excepciones solo para lo inesperado (BD caída, bug). El handler global las convierte en ProblemDetails (`standards/04`).

## Paginación
- `{Entity}Filter : PaginationParams` (`PageNumber`, `PageSize` con tope `MaxPageSize = 100`) se enlaza desde el query string.
- La respuesta es `PagedResult<T>` (`Items`, `PageNumber`, `PageSize`, `TotalCount`, `TotalPages`).
- **Siempre** con orden estable y desempate por `Id` (`OrderBy(e => e.Id)` al final de la spec).

## Especificaciones
```csharp
public sealed class InvoicesByFilterSpec : Specification<Invoice>
{
    public InvoicesByFilterSpec(InvoiceFilter filter)
    {
        Where(PredicateBuilder.True<Invoice>()
            .AndIf(filter.CustomerId.HasValue, e => e.CustomerId == filter.CustomerId)
            .AndIf(filter.IsActive.HasValue, e => e.IsActive == filter.IsActive));
        OrderByDescending(e => e.CreatedAt);
        OrderBy(e => e.Id);          // desempate
        ApplyPaging(filter);
    }
}
```
- Una clase por consulta, con nombre de negocio (`{Entity}ByIdSpec`, `{Entities}ByFilterSpec`, `Overdue{Entities}Spec`). Viven al final de `{Entity}Service.cs`.
- Por defecto `AsNoTracking`. Para modificar: `EnableTracking()`. Varias colecciones incluidas: `SplitQuery()`. Ver borrados: `IncludeSoftDeleted()`.
- `Include` solo de lo que la proyección o la regla necesita.
- Orden dinámico solo con **lista blanca** (`switch` sobre `SortBy`), nunca con un string arbitrario del cliente.
- `PredicateBuilder.True<T>().AndIf(cond, expr)` arma filtros opcionales sin `if` anidados; el resultado se traduce a SQL.
