# Baseline de seguridad

> **Aplica a:** Todos los perfiles. Es obligatorio también en la API pública (sin Identity).  
> **Propósito:** Protecciones mínimas de cualquier API del kit: OWASP API Top 10, headers, límites, secretos y API key opcional.  
> Índice general: `standards/00-INDEX.md`

## OWASP API Security Top 10 (2023) → cómo lo cubre el kit

| Riesgo | Protección en el kit | Qué debe hacer la IA en cada endpoint |
|---|---|---|
| **API1** Autorización a nivel de objeto (BOLA) | Specs filtradas por dueño/tenant cuando aplica | Si un recurso "pertenece" a alguien, filtrar por ese dueño **en la spec**, no después. Responder 404 si no es suyo |
| **API2** Autenticación rota | Identity + JWT corto + refresh con rotación, lockout, 2FA `[SEC]` | No inventar esquemas propios de login |
| **API3** Autorización a nivel de propiedad | Requests dedicados y DTOs de salida | Nunca enlazar entidades; no devolver campos internos (hashes, flags de sistema) |
| **API4** Consumo de recursos sin límite | Rate limiting global, `MaxPageSize`, límite de body en Kestrel | Toda lista paginada; archivos con `[RequestSizeLimit]` explícito |
| **API5** Autorización a nivel de función | `[HasPermission]` en cada acción `[SEC]` | Ninguna acción de escritura sin permiso (o sin API key en perfil público) |
| **API6** Acceso sin control a flujos sensibles | Rate limit estricto en Auth, respuestas genéricas | Flujos de negocio abusables (cupones, reservas) con límite propio |
| **API7** SSRF | — | Nunca hacer `HttpClient` a URLs que vengan del usuario sin lista blanca |
| **API8** Configuración insegura | Headers, HTTPS/HSTS, ProblemDetails sin detalles, OpenAPI solo en Development | No exponer `/scalar` ni `/openapi` en producción sin protección |
| **API9** Inventario deficiente | OpenAPI generado, versionado opcional | Documentar endpoints en el módulo; eliminar los obsoletos |
| **API10** Consumo inseguro de APIs | `HttpClient` tipado con resiliencia | Validar respuestas de terceros como si fueran input del usuario |

Referencia: https://owasp.org/API-Security/editions/2023/en/0x11-t10/

## Headers de seguridad — `Api/Middleware/SecurityHeadersMiddleware.cs`
→ Código: [`templates/api/src/KitApi.Api/Api/Middleware/SecurityHeadersMiddleware.cs`](../templates/api/src/KitApi.Api/Api/Middleware/SecurityHeadersMiddleware.cs)
HTTPS y HSTS se configuran en `Program.cs` (`standards/01`). El header `Server` se quita en Kestrel.

## Límites
- **Body:** 10 MB global en Kestrel. Endpoints de archivos: `[RequestSizeLimit(n)]` con el valor justo.
- **Paginación:** `PaginationParams.MaxPageSize = 100`. Nunca devolver listas completas de tablas que crecen.
- **Rate limiting:** global por IP (100/min) y estricto en Auth (10/min) `[SEC]`. Ajustar según `docs/04-non-functional-requirements.md`. Con proxy o balanceador, activar `ReverseProxy:Enabled` con los proxies conocidos (`standards/01`, `13a`) para usar la IP real.
- **Timeouts:** `HttpClient` a terceros siempre con timeout (`standards/11`).

## Secretos
- Desarrollo: `dotnet user-secrets`. Producción: variables de entorno o Azure Key Vault (`ai/suggestions-catalog.md`).
- Nunca en `appsettings*.json`, en el código, en logs ni en mensajes de error.
- Variables de entorno con doble guion bajo: `ConnectionStrings__Default`, `Jwt__SigningKey`.
- Rotar la llave JWT de forma planificada. Al rotarla, todos los access tokens vigentes dejan de servir.

## Datos sensibles
- **Nunca loguear**: contraseñas, tokens (access, refresh, reset, 2FA), API keys, números de tarjeta, documentos de identidad.
- Hash para secretos que solo se comparan (refresh tokens, API keys): SHA-256 + comparación en tiempo constante.
- Datos personales: solo los necesarios. Decisiones de retención en `docs/04-non-functional-requirements.md`.

## Dependencias
```bash
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```
Correr ambos en CI (`standards/13`). Un paquete con vulnerabilidad alta o crítica bloquea el merge.

## API key (opcional, sobre todo en perfil sin seguridad)
Para APIs públicas con clientes conocidos (integraciones máquina a máquina). Las lecturas pueden quedar abiertas y las escrituras protegidas.

`Api/Authentication/ApiKeyAuthentication.cs`
→ Código: [`templates/api/src/KitApi.Api/Api/Authentication/ApiKeyAuthentication.cs`](../templates/api/src/KitApi.Api/Api/Authentication/ApiKeyAuthentication.cs)

Registro (en `AddApi` o en un método `AddApiKeyAuthentication`):
```csharp
services.AddSingleton<IApiKeyStore, ConfigurationApiKeyStore>();
services.AddAuthentication(ApiKeyOptions.Scheme)
        .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(ApiKeyOptions.Scheme, null);
services.AddAuthorization();
// Program.cs: app.UseAuthentication(); app.UseAuthorization();
```
Uso: `[Authorize]` en las acciones de escritura (o en el controller, con `[AllowAnonymous]` en las lecturas). Rate limit por cliente: particionar por el claim `sub` en lugar de la IP.

Generar una clave y su hash (PowerShell):
```powershell
$key = [Convert]::ToBase64String((1..32 | % { Get-Random -Max 256 }) -as [byte[]])
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($key)))
"Clave para el cliente: $key"; "Hash para ApiKeys:Clients: $hash"
```

## Checklist de seguridad por endpoint
- [ ] Con seguridad: `[HasPermission]`. Sin seguridad: escritura protegida con API key o aprobada explícitamente como pública.
- [ ] Si el recurso tiene dueño, el filtro por dueño está en la spec.
- [ ] Entrada validada con FluentValidation; salida solo con DTO.
- [ ] Listas paginadas; archivos con límite de tamaño y tipo validado.
- [ ] Sin datos sensibles en logs ni en mensajes de error.
