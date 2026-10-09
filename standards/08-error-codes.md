# Códigos de error

> **Aplica a:** Todos los perfiles  
> **Propósito:** Formato único de errores de la API, reglas para los códigos y catálogo base.  
> Índice general: `standards/00-INDEX.md`

## Formato de respuesta (siempre ProblemDetails, RFC 9457)

Toda respuesta de error tiene `Content-Type: application/problem+json` y esta forma:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "No existe el registro 42.",
  "status": 404,
  "code": "{Entity}.NotFound",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

Errores de validación (400) con detalle por campo. Las claves van en camelCase, igual que el JSON:

```json
{
  "title": "Uno o más campos no son válidos.",
  "status": 400,
  "code": "Validation.Failed",
  "traceId": "…",
  "errors": {
    "name": [ "'Name' no debe estar vacío." ],
    "code": [ "La longitud de 'Code' debe ser menor o igual a 50." ]
  }
}
```

| Campo | Contenido | Uso por el cliente |
|---|---|---|
| `status` | Código HTTP | Decidir el flujo (401 → refrescar token; 409 → recargar) |
| `code` | Identificador **estable** del error | Lógica del cliente y textos traducidos. **Nunca** depender de `title` |
| `title` | Mensaje en español para mostrar | Mostrar al usuario si no hay traducción propia |
| `errors` | Solo en 400: campo → mensajes | Marcar los campos del formulario |
| `traceId` | Id de traza (W3C) | Mostrar en "Reportar problema"; buscarlo en los logs |

## Dónde se genera cada error

| Origen | Quién arma el ProblemDetails | Archivo |
|---|---|---|
| `Result` fallido de un service | `ApiControllerBase.ToProblem` | `standards/04` |
| FluentValidation | `ValidationFilter` (`code = Validation.Failed`) | `standards/04` |
| Excepción no controlada | `GlobalExceptionHandler` | `standards/04` |
| 401/403 de JWT, 404/405 de rutas | `UseStatusCodePages` + `AddProblemDetails` | `standards/01a` |
| 429 de rate limiting | `RateLimiter` (+ header `Retry-After`) | `standards/01a` |

## Reglas para los códigos
1. Formato: **`{Modulo}.{Motivo}`** en PascalCase y en inglés. Ejemplos: `Invoice.NotFound`, `Invoice.AlreadyPaid`, `Auth.LockedOut`.
2. El código es un **contrato**: una vez publicado no se renombra ni se reutiliza con otro significado. El `title` sí puede cambiar.
3. Cada módulo declara sus errores en `{Entity}Errors`, y cada uno tiene `ErrorType`, que define el HTTP.
4. Un error por causa: no usar códigos genéricos (`Invoice.Error`) para casos distintos.
5. Los errores de autenticación son **genéricos a propósito**: no revelan si un email existe o si una contraseña casi coincide.
6. Nunca incluir en `title` datos sensibles, SQL, stack traces ni el mensaje de una excepción.
7. Todo código nuevo se agrega a la tabla de errores del módulo en `docs/modules/<módulo>.md`.

## `ErrorType` → HTTP

| `ErrorType` | HTTP | Cuándo |
|---|---|---|
| `Validation` | 400 | Datos con forma correcta pero inválidos para el negocio (FK inexistente, fecha fuera de rango) |
| `Unauthorized` | 401 | Credenciales o tokens inválidos |
| `Forbidden` | 403 | Autenticado pero sin permiso, o cuenta bloqueada o desactivada |
| `NotFound` | 404 | El recurso no existe (o no es visible para el usuario) |
| `Conflict` | 409 | Duplicados, concurrencia, estado incompatible ("ya pagada") |
| `Failure` | 400 | Regla de negocio que no encaja en las anteriores |

> Si un usuario no debe saber que un recurso existe (por ejemplo, datos de otro cliente), responder **404**, no 403.

## Catálogo base (lo genera la infraestructura)

| Código | HTTP | Origen |
|---|---|---|
| `Validation.Failed` | 400 | `ValidationFilter` |
| `Request.Invalid` | 400 | JSON mal formado o petición inválida (`BadHttpRequestException`) |
| `Concurrency.Conflict` | 409 | `DbUpdateConcurrencyException` |
| `Request.Cancelled` | 499 | El cliente canceló la petición |
| `Server.Error` | 500 | Excepción no controlada |
| *(sin `code`)* | 401 / 403 / 404 / 405 / 429 | Respuestas del framework; el cliente decide por `status` |

Con seguridad, el catálogo de Auth (`Auth.*`, `User.*`, `Role.*`) está en `standards/05b-auth-contracts.md`.

## Plantilla por entidad
```csharp
public static class {Entity}Errors
{
    public static Error NotFound(int id) => Error.NotFound("{Entity}.NotFound", $"No existe el registro {id}.");
    public static Error CodeAlreadyExists(string code) => Error.Conflict("{Entity}.CodeAlreadyExists", $"Ya existe un registro con código '{code}'.");
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("{Entity}.Concurrency", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
    // Reglas de negocio propias: Error.Conflict("{Entity}.<Motivo>", "<mensaje>") o Error.Validation(...)
}
```

## Documentar en el módulo
En `docs/modules/<módulo>.md`, sección "Errores":

| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `{Entity}.NotFound` | 404 | El id no existe o está eliminado |
| `{Entity}.CodeAlreadyExists` | 409 | Ya hay otro registro activo con ese código |
