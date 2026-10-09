# Integración con el frontend

> **Aplica a:** Todos los perfiles (contrato de tokens solo con seguridad)  
> **Propósito:** Lo que **la API** configura para que cualquier frontend (Vue, React, Angular, móvil) la consuma cómodo y sin sorpresas. El frontend en sí está fuera del alcance del kit.  
> Índice general: `standards/00-INDEX.md`

## Resumen del contrato

| Tema | Decisión |
|---|---|
| Formato | JSON (`application/json`); errores en `application/problem+json` |
| Nombres de propiedades | **camelCase** (default de ASP.NET Core) |
| Enums | **Como texto** (`"Active"`, no `1`), con `JsonStringEnumConverter` (`standards/01a`) |
| Fechas con hora | ISO 8601 en **UTC con `Z`** (`2026-10-09T14:30:00Z`); el cliente convierte a hora local |
| Fechas sin hora | `DateOnly` → `"2026-10-09"` (cumpleaños, vencimientos) |
| Decimales | Número JSON. Montos con `HasPrecision(18, 2)` en BD |
| Ids | `int` en entidades de negocio; `Guid` (string) en usuarios y roles |
| `null` | Las propiedades nulas **se envían** como `null` (forma estable para tipar en TypeScript) |
| Errores | ProblemDetails con `code`, `title`, `traceId` y `errors` por campo (`standards/08`) |
| Paginación | Objeto `PagedResult` en el body (abajo) |
| Autenticación `[SEC]` | `Authorization: Bearer {accessToken}` + refresh token rotativo (`standards/05f`) |

## CORS
Ya configurado en `standards/01a`:
- Orígenes exactos desde `Cors:AllowedOrigins`. Nunca `AllowAnyOrigin` en producción.
- Sin `AllowCredentials`: la API usa tokens Bearer, no cookies.
- Headers expuestos al navegador: `X-Trace-Id`, `Retry-After` y `Content-Disposition` (nombre de archivo en descargas).
- En desarrollo, agregar el origen del dev server del frontend (por ejemplo `https://localhost:5173`) y confiar en el certificado local: `dotnet dev-certs https --trust`.

## Fechas en UTC
- Se guardan en UTC (`TimeProvider.GetUtcNow()`).
- `UtcDateTimeConverter` (`standards/02`) marca las fechas leídas de SQL Server como UTC, para que salgan con `Z`. Sin eso, el navegador las interpretaría como hora local.
- Filtros de fecha recibidos del cliente: aceptar ISO 8601 con zona y convertir a UTC antes de consultar.

## Paginación
Request por query string (`PaginationParams`): `?pageNumber=1&pageSize=20&search=...&sortBy=name&sortDescending=false`.

Respuesta (`PagedResult<T>`):
```json
{
  "items": [ { "id": 1, "name": "…" } ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 135,
  "totalPages": 7,
  "hasPrevious": false,
  "hasNext": true
}
```
`pageSize` máximo: 100. Valores fuera de rango se ajustan, no fallan.

## Errores para formularios
- 400 con `errors`: las claves están en camelCase y coinciden con las propiedades del JSON enviado (`name`, `{parent}Id`), así el cliente marca cada campo directamente.
- Colecciones anidadas: `items[0].quantity`.
- Lógica del cliente basada en `status` y `code`, nunca en el texto de `title`.

## OpenAPI para generar el cliente
- Documento en `/openapi/v1.json` (solo en Development; ver `standards/09`).
- Cada acción declara sus respuestas con `[ProducesResponseType<T>(...)]` para que los tipos generados sean exactos.
- Generadores recomendados: **Kiota** (de Microsoft), **NSwag**, **openapi-typescript** u **orval**. El cliente generado se versiona en el repositorio del frontend.
- Para generar el documento en la compilación (sin levantar la API): paquete `Microsoft.Extensions.ApiDescription.Server`, que deja el JSON en la salida del build. Verificar la configuración vigente en la documentación de `Microsoft.AspNetCore.OpenApi`.
- Los enums se publican como `string` con sus valores posibles gracias a `JsonStringEnumConverter`.

## Contrato de autenticación `[SEC]`
1. `POST /api/auth/login`. Si responde `tokens`, guardarlos. Si responde `requiresTwoFactor: true`, pedir el código y llamar `POST /api/auth/login/2fa` con `twoFactorToken`.
2. Cada request: `Authorization: Bearer {accessToken}`. El access token dura 15 minutos.
3. Ante un **401**: llamar `POST /api/auth/refresh` **una sola vez**, con un bloqueo para que peticiones simultáneas no refresquen en paralelo. Si el refresh falla, cerrar sesión local.
4. Cada refresh devuelve un refresh token **nuevo**. Reutilizar uno viejo revoca toda la sesión.
5. Logout: `POST /api/auth/logout` con el refresh token.
6. Los enlaces de los emails apuntan al frontend (`App:ClientUrl`): `/confirm-email`, `/reset-password`, `/confirm-email-change`. El frontend lee el query string y hace el POST al endpoint correspondiente.
7. Permisos para mostrar u ocultar opciones: `GET /api/account/me` devuelve `roles` y `permissions`. La API **siempre** vuelve a validar; ocultar botones es solo usabilidad.

## Archivos
- **Subida:** `multipart/form-data` con `IFormFile`, `[RequestSizeLimit]` explícito y validación de extensión y tipo real. Responder con el DTO del archivo guardado.
- **Descarga:** `return File(stream, contentType, fileName);`. El nombre viaja en `Content-Disposition`, que ya está expuesto por CORS.
- Archivos grandes: URL firmada de almacenamiento (Azure Blob SAS) en vez de pasar por la API.

## Rate limiting
- 429 con header `Retry-After` (segundos). El cliente debe esperar ese tiempo antes de reintentar.

## Operaciones largas
- Si una operación tarda más de unos segundos (importaciones, reportes), responder **202 Accepted** con un id y exponer `GET /api/{recurso}/{id}/status`, o notificar por SignalR (`ai/suggestions-catalog.md`).

## Soporte
- Cada respuesta trae `X-Trace-Id`. El frontend debe mostrarlo en las pantallas de error ("Código de soporte: …") para buscarlo en los logs (`standards/10`).
