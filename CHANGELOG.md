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

### Eliminado
- `AI_GUIDE_API_ARCHITECTURE.md`: su contenido completo pasó a `standards/`.

### Pendiente (próximos lotes)
- Standards `08`–`11`, `14`, `15`; plantillas de `docs/`; roles (`ai/roles/`) y flujos (`ai/workflows/`) con sus adaptadores de Claude Code; `settings.json`; plantilla ejecutable verificada.
