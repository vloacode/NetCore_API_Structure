# Changelog del kit

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/). El kit usa versionado semántico:
- **MAJOR**: cambio incompatible en standards (los proyectos deben migrar código).
- **MINOR**: standards, flujos o roles nuevos compatibles.
- **PATCH**: correcciones de texto o de ejemplos.

## [0.1.0] - 2026-10-09 (en construcción)
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

### Cambiado
- `12-testing`: los services se prueban contra SQL Server real (Testcontainers); SQLite/InMemory no son compatibles con el modelo.

### Eliminado
- `AI_GUIDE_API_ARCHITECTURE.md`: su contenido completo pasó a `standards/`.

### Pendiente (próximos lotes)
- Roles (`ai/roles/`) y flujos (`ai/workflows/`) con sus adaptadores de Claude Code; `settings.json`; plantilla ejecutable verificada.
