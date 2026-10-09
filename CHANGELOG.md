# Changelog del kit

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). El kit usa versionado semántico:
- **MAJOR**: cambio incompatible en standards (los proyectos deben migrar código).
- **MINOR**: standards, flujos o roles nuevos compatibles.
- **PATCH**: correcciones de texto o de ejemplos.

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

