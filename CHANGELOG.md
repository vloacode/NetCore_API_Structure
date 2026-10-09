# Changelog del kit

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). El kit usa versionado semántico:
- **MAJOR**: cambio incompatible en standards (los proyectos deben migrar código).
- **MINOR**: standards, flujos o roles nuevos compatibles.
- **PATCH**: correcciones de texto o de ejemplos.

## [2.3.0] - 2026-10-09
### Añadido
- **Analítica de producto (uso)**, capacidad opcional `product-analytics`:
  - `standards/17`:
    - `IAnalyticsTracker` que no bloquea, con cola `Channel` y `AnalyticsDispatcher` en segundo plano.
    - Destinos `IAnalyticsSink`: BD por defecto, log en desarrollo y otros con ADR.
    - Tabla `AnalyticsEvents` con JSON (`jsonb` en PostgreSQL), identidad seudónima con HMAC, guardia de datos personales y retención automática.
  - `standards/17a`: endpoint `POST /api/analytics/events` para el frontend (lista blanca, máximo 50 eventos, rate limit propio, `X-Anonymous-Id`), consultas SQL de KPIs y tests de integración.
  - `docs/11-product-analytics.md` (preguntas → KPIs → catálogo de eventos), flujo y comando `/analytics-event`, product-analyst amplía su alcance y entra en el catálogo de sugerencias y en `/project-init`.
  - GA4 no se incluye en el kit (es del frontend). `standards/17` explica cómo agregarlo como destino con un ADR.
- Marca `// [CAP:<id>]` para código que solo existe con una capacidad activa.

## [2.2.0] - 2026-10-09
### Añadido
- **Opción de despliegue con Nginx** (`Deployment = nginx`, servidor Linux/VM con Docker): `standards/13a-reverse-proxy-nginx.md`. Incluye:
  - `nginx.conf` con TLS, HTTP/2, límites, gzip y WebSockets.
  - `docker-compose` con red interna de subred fija.
  - Certificados con certbot, comandos y checklist.
- **Forwarded Headers en la API** (`standards/01a`):
  - Configuración `ReverseProxy:Enabled`, `KnownProxies` y `KnownNetworks`, desactivada por defecto.
  - `ForwardLimit = 1`, para que el cliente no pueda falsificar su IP.
  - Sirve con cualquier proxy o balanceador.
- Nginx, YARP y Caddy en el catálogo de sugerencias. Referencias oficiales de Nginx y de proxies/balanceadores en `ai/references.md`.
- `scripts/verify/verify-nginx.py`: prueba la API detrás de un contenedor Nginx real (8/8).

## [2.1.0] - 2026-10-09
### Verificado
- PostgreSQL de punta a punta (contenedor PostgreSQL 18): 32/32 pruebas con seguridad y 8/8 en el perfil público, igual que SQL Server.
- Tests de integración de `standards/12` ejecutados con Testcontainers (PostgreSQL): 3/3 con seguridad, 2/2 en el perfil público.

### Añadido
- `verify-standards.py --tests` / `--run-tests`: genera, compila y ejecuta el proyecto de tests de `standards/12`. La CI lo corre en cada push.

### Corregido (encontrado al ejecutar los tests por primera vez)
- `standards/12`:
  - El ejemplo usaba `SeedParentAsync()` sin definirlo. Ahora `ApiFactory.SeedAsync<T>()` siembra con el DbContext real.
  - Respawn para PostgreSQL estaba solo como nota. Ahora va en el código, con marcas por motor.
  - Con seguridad, Respawn borraba el admin. `ResetDatabaseAsync` vuelve a sembrarlo.
  - Imágenes de Testcontainers explícitas y configuración JWT de tests completa.
- La plantilla `dotnet new xunit` de .NET 10 trae xUnit v2: pasos para cambiar a `xunit.v3` con `OutputType Exe`.
- `dotnet test` con xUnit v3 en el SDK de .NET 10 exige `"test": { "runner": "Microsoft.Testing.Platform" }` en `global.json` (`standards/14`).
- Comandos de filtro y cobertura actualizados a Microsoft.Testing.Platform (`--filter-class`, `--filter-method`, `--coverage`), también en la CI de `standards/13`.
- `tests/.editorconfig`: `CA1707` y `CA1711` desactivadas en proyectos de tests (nombres `Metodo_Escenario_Resultado` y fixtures `*Collection` de xUnit).

## [2.0.0] - 2026-10-09
### Añadido
- **Soporte para PostgreSQL** además de SQL Server: campo `Database` en el perfil, marcas `// [MSSQL]` / `// [PGSQL]`, clase `SqlDialect` (filtros de índices por motor), proveedor Npgsql + `EFCore.NamingConventions` (snake_case), búsquedas con `ILike`, Testcontainers/Respawn para PostgreSQL, docker-compose y Azure Database for PostgreSQL.
- **Versiones futuras de .NET**: campo `TargetFramework` en el perfil, `standards/16-framework-versions.md` (política LTS, piezas dependientes de la versión, adopción y migración), flujo y comando `/upgrade-dotnet`, propiedades `MicrosoftVersion` / `NpgsqlEfVersion` en `Directory.Packages.props` y `verify-standards.py --framework`.
- **Referencias oficiales para la IA**: `ai/references.md` (≈90 enlaces verificados por tema y cómo buscar) y MCP de Microsoft Learn configurado para Claude Code (`.mcp.json`), Cursor (`.cursor/mcp.json`) y VS Code/Copilot (`.vscode/mcp.json`).
- Verificador: matriz perfil × motor, migración `InitialCreate` en cada combinación, siembra de prueba sin depender de `sqlcmd`, pruebas de humo para PostgreSQL (`--smoke-postgresql`).

### Cambiado (incompatible)
- **Concurrencia optimista**: `RowVersion` pasa de `byte[]` (`rowversion`, solo SQL Server) a `Guid` gestionado por el interceptor (`IVersioned`, `IsConcurrencyToken`). Funciona igual en ambos motores. El DTO sigue enviando `rowVersion` como string.
- Sin defaults SQL de fecha (`SYSUTCDATETIME()`): el interceptor asigna las fechas.
- Las plantillas ya no fijan nombres de tabla con `ToTable(...)`: el nombre sale del `DbSet` (y en PostgreSQL pasa a snake_case).

### Migración desde 1.0.0 (proyectos ya creados)
1. Seguir `upgrade-kit` para traer los standards nuevos.
2. En `BaseEntity`, cambiar `RowVersion` a `Guid` e implementar `IVersioned`; en `BaseEntityConfiguration`, `IsConcurrencyToken()` en vez de `IsRowVersion()`; agregar la asignación de `RowVersion` en el interceptor.
3. Actualizar mapeos (`e.RowVersion.ToString()`), validadores (`Guid.TryParse`) y la comparación del service.
4. Crear una migración y revisar el SQL: la columna pasa de `rowversion` a `uniqueidentifier` (inicializar con `NEWID()` en los registros existentes).

## [1.0.0] - 2026-10-09
### Añadido
- Lote 7, verificación real: `scripts/verify/` (`verify-standards.py`, `materialize.py`, pruebas de humo).
  - El código de `standards/` se compila en ambos perfiles en Release, con analizadores, warnings como errores y `dotnet format`.
  - Con `--smoke` se prueba contra SQL Server: 32 pruebas con seguridad y 8 en el perfil público.
  - Job `verify-standards` en CI. `.gitignore` del kit.
- Marca `// [PUB]` (solo sin seguridad) como contraparte de `// [SEC]`.

### Corregido (encontrado por la verificación)
- `LoggingEmailSender` estaba en un standard solo de seguridad, pero se registra en ambos perfiles: movido a `01a`.
- El perfil público no tenía cómo aplicar migraciones al iniciar: nueva clave común `Database:ApplyMigrationsOnStartup` en `Program.cs`; el seeder queda solo para roles y admin.
- Faltaba el `using` de `SystemCurrentUserService` en el perfil público; `AppDbContext` y la configuración de OpenAPI ahora tienen una línea por perfil (`[SEC]` / `[PUB]`).
- Configuraciones EF de Identity separadas en `IdentityConfigurations.cs` (`[SEC]`).
- `traceId` inconsistente en los 404 del framework (`00-…-00`): ahora siempre igual a `X-Trace-Id`.
- Analizadores en Release:
  - `CultureInfo.InvariantCulture` en `Retry-After`.
  - `Repository.Set` pasa a ser propiedad.
  - Nombre de parámetro en `GlobalExceptionHandler`.
  - Reglas ajustadas y documentadas en `standards/14`.
  - Migraciones excluidas del análisis.
- `dotnet format` obligatorio tras generar código: el orden de los `using` depende del nombre del proyecto. Documentado en `standards/14`, `project-init`, `new-entity` y la Definition of Done.

## [0.1.0] - 2026-10-09
### Añadido
- `AGENTS.md` como entrada universal para cualquier IA, y `CLAUDE.md` que lo importa.
- Adaptadores para GitHub Copilot (`.github/copilot-instructions.md`) y Cursor (`.cursor/rules/kit.mdc`).
- `ai/context-packs.md` (qué leer para cada tarea) y `ai/suggestions-catalog.md` (capacidades opcionales con referencias).
- `standards/00`–`07a`, `12`, `13` y `99`, divididos de la guía única anterior, con soporte de perfiles con y sin seguridad (marca `[SEC]`).

- Lote 2: standards `08` (códigos de error), `09` (baseline de seguridad, OWASP API Top 10, headers, API key), `10` (observabilidad: logging, OpenTelemetry, health checks, `X-Trace-Id`), `11` (rendimiento), `14` (estándares de código, `Directory.Build.props`, gestión central de paquetes, `.editorconfig`), `15` (integración con el frontend). `12` (testing con Testcontainers) y `13` (Docker, CI, migraciones, Azure/IIS) reescritos.
- Ajustes transversales: `traceId` y `code` en todo ProblemDetails, claves de validación en camelCase, fechas UTC con `Z`, headers expuestos por CORS, `Retry-After` en 429, límites de Kestrel, health checks y estructura `src/` + `tests/` con `.slnx`.
- `.gitattributes`.
- Lote 3: plantillas de `docs/` (`00-MASTER_CONTEXT` con Perfil del proyecto, `01`–`10`, plantilla de módulo, ADR, runbook, `ai/AI_MEMORY`, `ai/PROJECT_STATUS`). Marcadores de proyecto `{{...}}` e instrucciones para la IA en comentarios `<!-- IA: -->`.
- Ids de capacidades en `ai/suggestions-catalog.md` para el campo `Capabilities` del perfil.
- Lote 4: 9 roles en `ai/roles/` (product-analyst, solution-architect, database-architect, backend-developer, qa-engineer, security-reviewer, devops-engineer, observability-engineer, technical-writer) y sus subagentes en `.claude/agents/` (revisor de seguridad en solo lectura).
- Lote 5: 15 flujos en `ai/workflows/` (project-init con elección de modo y sugerencias proactivas con búsqueda web, new-module, new-entity, new-feature, api-endpoint, database-change, test-generation, code-review, security-review, performance-review, log-analysis, incident-response, record-decision, docs-sync, upgrade-kit) y sus skills en `.claude/skills/` (comandos `/...`).
- Lote 6: `.claude/settings.json` (permisos y hooks), hooks `scripts/hooks/guard-edits.py` (secretos en appsettings, archivos del kit) y `scripts/hooks/build-on-stop.py` (no terminar sin compilar), `scripts/check-kit.py` y workflow `kit-checks`.

### Cambiado
- `12-testing`: los services se prueban contra SQL Server real (Testcontainers); SQLite/InMemory no son compatibles con el modelo.

### Eliminado
- `AI_GUIDE_API_ARCHITECTURE.md`: su contenido completo pasó a `standards/`.

