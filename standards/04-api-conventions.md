# Convenciones de la API

> **Aplica a:** Todos los perfiles  
> **Propósito:** Controllers, mapeo Result→HTTP, validación automática, manejo global de excepciones, OpenAPI y JSON.  
> **Código:** [`Api/`](../templates/api/src/KitApi.Api/Api/) · [`Extensions/ApiExtensions.cs`](../templates/api/src/KitApi.Api/Extensions/ApiExtensions.cs)

## Controllers
- Todo controller hereda `ApiControllerBase` (`[ApiController]`, ruta `api/[controller]`) y es **delgado**: recibe, llama al service y devuelve `HandleResult(...)` o `HandleCreated(...)` (201 con `Location`).
- Sin lógica de negocio, sin `DbContext`, sin try/catch.
- Los controllers de negocio viven en su feature (`Features/{Entities}/{Entities}Controller.cs`); los del kit (Auth, Account, Users, Roles, Analytics) en `Api/Controllers/`.
- Cada acción declara `[ProducesResponseType<T>]` para OpenAPI y recibe `CancellationToken ct`.
- Con seguridad: `[HasPermission(...)]` en **cada** acción. Sin seguridad: público, o `[Authorize]` (API key) en escrituras.

## Result → HTTP

| `ErrorType` | HTTP |
|---|---|
| (éxito con valor) | 200 OK / 201 Created |
| (éxito sin valor) | 204 No Content |
| `Validation` / `Failure` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |

Todas las respuestas de error son **ProblemDetails** (RFC 9457) con `code` y `traceId` (`standards/08`).

## Validación automática
`ValidationFilter` ejecuta el `IValidator<T>` de cada argumento y responde **400** con `errors` en camelCase (`{ "name": ["..."] }`) antes de entrar a la acción. No se valida a mano en el controller.

## Excepciones (`GlobalExceptionHandler`)

| Excepción | HTTP | `code` |
|---|---|---|
| `DbUpdateConcurrencyException` | 409 | `Concurrency.Conflict` |
| `OperationCanceledException` | 499 | `Request.Cancelled` |
| `BadHttpRequestException` | su status | `Request.Invalid` |
| cualquier otra | 500 (log de error) | `Server.Error` |

Nunca se expone `exception.Message` al cliente.

## OpenAPI
`Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) + Scalar (`/scalar`), solo en Development. Con seguridad, `BearerSecuritySchemeTransformer` agrega el esquema Bearer. .NET 10 usa **Microsoft.OpenApi 2.x** (namespace `Microsoft.OpenApi`, sin `.Models`).

## JSON
camelCase (default) + enums como texto (`JsonStringEnumConverter`). Fechas UTC con `Z` (`standards/15`).
