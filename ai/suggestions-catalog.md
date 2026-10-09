# Catálogo de sugerencias para APIs .NET

Úsalo en el paso de **sugerencias proactivas** de `project-init` y cada vez que una conversación toque estos temas.

**Cómo usarlo:**
1. Revisa lo que dijo el usuario contra los **disparadores** de cada capacidad.
2. Propón solo las que encajan, explicando en una línea el porqué para *este* proyecto.
3. Si tienes acceso web, confirma la referencia y la **versión vigente del paquete** (nuget.org, Microsoft Learn) y da el enlace. Si no, usa el enlace de aquí y marca "verificar versión".
4. Registra cada sugerencia aceptada o descartada en el ADR inicial (`docs/decisions/`).

Formato: **Disparadores** · **Qué agrega** · **Paquetes** · **Referencia**. El id entre comillas invertidas es el que se anota en `Capabilities` del Perfil del proyecto (`docs/00-MASTER_CONTEXT.md`).

> `health-checks` ya viene incluido en todos los perfiles (`standards/01a`); se lista para recordar agregar checks de dependencias.

---

## Acceso y seguridad

### API key para clientes conocidos · `api-key`
- **Disparadores:** API pública con escrituras; integraciones máquina-a-máquina; "solo ciertos sistemas pueden llamar".
- **Qué agrega:** handler de autenticación propio que valida el header `X-Api-Key` contra claves guardadas con hash. Rate limit por cliente.
- **Paquetes:** ninguno (ASP.NET Core).
- **Referencia:** https://learn.microsoft.com/aspnet/core/security/authentication/

### Azure Key Vault para secretos · `key-vault`
- **Disparadores:** despliegue en Azure; varios entornos; auditoría de secretos.
- **Qué agrega:** los secretos (llave JWT, cadenas de conexión) se leen de Key Vault como configuración.
- **Paquetes:** `Azure.Extensions.AspNetCore.Configuration.Secrets`, `Azure.Identity`.
- **Referencia:** https://learn.microsoft.com/aspnet/core/security/key-vault-configuration

### Llaves de Data Protection persistentes · `data-protection-keys`
- **Disparadores:** perfil con seguridad + más de una instancia, contenedores o reinicios frecuentes.
- **Qué agrega:** los tokens de email y reset siguen siendo válidos tras reinicios o con varias réplicas.
- **Paquetes:** `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (o Azure Blob/Key Vault).
- **Referencia:** https://learn.microsoft.com/aspnet/core/security/data-protection/introduction

## Diseño de la API

### Versionado de API · `api-versioning`
- **Disparadores:** API pública; clientes móviles; "habrá cambios incompatibles"; varios consumidores externos.
- **Qué agrega:** rutas `/api/v1/...`, versiones en OpenAPI y deprecación ordenada.
- **Paquetes:** `Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer`.
- **Referencia:** https://github.com/dotnet/aspnet-api-versioning

### Idempotency keys · `idempotency`
- **Disparadores:** pagos; creación de pedidos; clientes con reintentos automáticos; redes inestables (móvil).
- **Qué agrega:** header `Idempotency-Key`; la misma petición repetida devuelve la respuesta original sin duplicar.
- **Paquetes:** ninguno (filtro + tabla) o caché distribuida.
- **Referencia:** https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/

### Webhooks (salientes y entrantes) · `webhooks`
- **Disparadores:** "avisar a otros sistemas"; integración con pasarelas de pago, CRM o ERP.
- **Qué agrega:** suscripciones, firma HMAC, reintentos con backoff y registro de entregas. Para webhooks entrantes, verificación de firma.
- **Paquetes:** ninguno; para reintentos, `Microsoft.Extensions.Http.Resilience`.
- **Referencia:** https://www.standardwebhooks.com/

### Exportación CSV / Excel · `export`
- **Disparadores:** "descargar reportes"; "exportar a Excel"; usuarios administrativos.
- **Qué agrega:** endpoints que devuelven archivos con streaming.
- **Paquetes:** `CsvHelper`, `ClosedXML`.
- **Referencia:** https://joshclose.github.io/CsvHelper/ · https://github.com/ClosedXML/ClosedXML

## Datos

### Auditoría detallada (historial de cambios) · `temporal-audit`
- **Disparadores:** datos regulados (salud, finanzas); "quién cambió qué y cuándo"; requisitos legales.
- **Qué agrega:** historial completo por fila. Opción recomendada: **tablas temporales de SQL Server**, que EF Core soporta de forma nativa.
- **Paquetes:** ninguno (EF Core SQL Server).
- **Referencia:** https://learn.microsoft.com/ef/core/providers/sql-server/temporal-tables

### Multi-tenant · `multi-tenant`
- **Disparadores:** SaaS; "varias empresas usan el sistema"; datos aislados por cliente.
- **Qué agrega:** `TenantId` en las entidades, filtro global por tenant y resolución del tenant por token o subdominio.
- **Paquetes:** ninguno (EF Core query filters).
- **Referencia:** https://learn.microsoft.com/ef/core/miscellaneous/multitenancy

### Búsqueda de texto · `full-text-search`
- **Disparadores:** "buscar por nombre o descripción" en tablas grandes; búsqueda tolerante.
- **Qué agrega:** índices full-text de SQL Server con `EF.Functions.Contains` / `FreeText`.
- **Paquetes:** ninguno.
- **Referencia:** https://learn.microsoft.com/sql/relational-databases/search/full-text-search

### Almacenamiento de archivos · `file-storage`
- **Disparadores:** subir documentos, imágenes o adjuntos.
- **Qué agrega:** interfaz `IFileStorage` (disco local en desarrollo, Azure Blob en producción), límites de tamaño, validación de tipo y URLs firmadas.
- **Paquetes:** `Azure.Storage.Blobs`.
- **Referencia:** https://learn.microsoft.com/azure/storage/blobs/storage-quickstart-blobs-dotnet

## Rendimiento

### Caché (HybridCache / output caching) · `caching`
- **Disparadores:** catálogos que cambian poco; lecturas muy frecuentes; API pública con mucho tráfico.
- **Qué agrega:** `HybridCache` (memoria + distribuida, protección contra estampida) en services; output caching para respuestas GET completas.
- **Paquetes:** `Microsoft.Extensions.Caching.Hybrid`; para Redis, `Microsoft.Extensions.Caching.StackExchangeRedis`.
- **Referencia:** https://learn.microsoft.com/aspnet/core/performance/caching/hybrid · https://learn.microsoft.com/aspnet/core/performance/caching/output

### Compresión de respuestas · `response-compression`
- **Disparadores:** respuestas JSON grandes; clientes móviles. Solo si no lo hace ya el proxy o el servidor.
- **Qué agrega:** Brotli/Gzip.
- **Paquetes:** ninguno.
- **Referencia:** https://learn.microsoft.com/aspnet/core/performance/response-compression

## Procesos y mensajería

### Jobs en segundo plano · `background-jobs`
- **Disparadores:** tareas programadas (limpiezas, recordatorios, cierres diarios); procesos largos; importaciones.
- **Qué agrega:** `BackgroundService` para lo simple; Hangfire si se necesitan reintentos, panel y cron.
- **Paquetes:** ninguno o `Hangfire.AspNetCore` + `Hangfire.SqlServer`.
- **Referencia:** https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services · https://www.hangfire.io/

### Outbox / eventos de dominio · `outbox`
- **Disparadores:** "cuando pase X, hacer Y" con garantía; webhooks o emails que no deben perderse; integración entre servicios.
- **Qué agrega:** tabla outbox escrita en la misma transacción y un procesador en segundo plano que publica.
- **Paquetes:** ninguno (patrón) o un bus de mensajes si ya existe.
- **Referencia:** https://microservices.io/patterns/data/transactional-outbox.html

### Email real (SMTP) · `email-smtp`
- **Disparadores:** perfil con seguridad (confirmaciones, reset); notificaciones a usuarios.
- **Qué agrega:** `IEmailSender` con SMTP y plantillas HTML. Reemplaza al `LoggingEmailSender`.
- **Paquetes:** `MailKit`.
- **Referencia:** https://github.com/jstedfast/MailKit

### Tiempo real · `realtime-signalr`
- **Disparadores:** notificaciones en vivo; tableros que se actualizan; chat; estado de procesos.
- **Qué agrega:** hub de SignalR autenticado con el mismo JWT.
- **Paquetes:** ninguno (incluido en ASP.NET Core).
- **Referencia:** https://learn.microsoft.com/aspnet/core/signalr/introduction

### Llamadas HTTP resilientes a terceros · `http-resilience`
- **Disparadores:** integración con APIs externas (pagos, gobierno, proveedores).
- **Qué agrega:** `HttpClient` tipado con reintentos, timeout y circuit breaker.
- **Paquetes:** `Microsoft.Extensions.Http.Resilience`.
- **Referencia:** https://learn.microsoft.com/dotnet/core/resilience/http-resilience

## Operación

### Observabilidad con OpenTelemetry · `opentelemetry`
- **Disparadores:** producción; varios servicios; "saber por qué está lento".
- **Qué agrega:** trazas, métricas y logs exportados (Azure Monitor, Grafana, Seq…).
- **Paquetes:** `OpenTelemetry.Extensions.Hosting`, instrumentaciones de AspNetCore, Http y EF Core.
- **Referencia:** https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel

### Health checks · `health-checks`
- **Disparadores:** contenedores, Kubernetes, Azure App Service o cualquier balanceador.
- **Qué agrega:** `/health/live` y `/health/ready`, con chequeo de base de datos.
- **Paquetes:** `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.
- **Referencia:** https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks

### .NET Aspire (desarrollo local) · `aspire`
- **Disparadores:** la API depende de SQL Server, Redis u otros servicios; se quiere un panel local de logs y trazas.
- **Qué agrega:** AppHost que levanta las dependencias en contenedores y un dashboard de observabilidad.
- **Paquetes:** plantillas y paquetes de Aspire.
- **Referencia:** https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview

### Feature flags · `feature-flags`
- **Disparadores:** lanzamientos graduales; funciones por cliente; apagar algo sin redeploy.
- **Qué agrega:** `IFeatureManager` y atributos `[FeatureGate]`.
- **Paquetes:** `Microsoft.FeatureManagement.AspNetCore`.
- **Referencia:** https://learn.microsoft.com/azure/azure-app-configuration/feature-management-dotnet-reference

### Localización (varios idiomas) · `localization`
- **Disparadores:** usuarios en varios idiomas; mensajes de error traducidos.
- **Qué agrega:** mensajes de `{Entity}Errors` y validaciones en recursos `.resx` según `Accept-Language`.
- **Paquetes:** ninguno.
- **Referencia:** https://learn.microsoft.com/aspnet/core/fundamentals/localization

---

## Combinaciones frecuentes
| Si el proyecto es… | Sugerir |
|---|---|
| API pública | Versionado, caché, rate limiting por cliente, API key si hay escrituras |
| Con pagos | Idempotency keys, webhooks, outbox, auditoría detallada |
| SaaS | Multi-tenant, feature flags, Data Protection persistente |
| Con archivos | Almacenamiento de archivos, límites de tamaño, jobs para procesarlos |
| Datos sensibles (salud, finanzas) | Auditoría detallada, Key Vault, revisión de seguridad temprana |
