@AGENTS.md

# Específico de Claude Code

Todo lo de `AGENTS.md` aplica. Esta sección solo agrega cómo usar las capacidades propias de Claude Code.

## Comandos (skills)
Cada flujo de `ai/workflows/` está expuesto como comando en `.claude/skills/<flujo>/SKILL.md`:

`/project-init` · `/new-module` · `/new-entity` · `/new-feature` · `/api-endpoint` · `/database-change` · `/test-generation` · `/code-review` · `/security-review` · `/performance-review` · `/log-analysis` · `/incident-response` · `/record-decision` · `/docs-sync` · `/upgrade-kit`

El contenido real está en `ai/workflows/`. Las skills solo apuntan ahí: si cambias un flujo, edita `ai/workflows/`, no la skill.

## Subagentes
Cada rol de `ai/roles/` tiene un subagente en `.claude/agents/<rol>.md`:
`product-analyst`, `solution-architect`, `database-architect`, `backend-developer`, `qa-engineer`, `security-reviewer`, `devops-engineer`, `observability-engineer`, `technical-writer`.

## Orquestación
- **La sesión principal es el orquestador**: clasifica la tarea, elige el flujo, coordina subagentes y habla con el usuario. Los subagentes no pueden lanzar otros subagentes.
- Delega en un subagente cuando la tarea es autocontenida (implementar una entidad, revisar seguridad, escribir tests) y su resultado se puede resumir.
- Los revisores (`security-reviewer`, `qa-engineer` en modo revisión) trabajan en **solo lectura** y devuelven hallazgos; los cambios los aplica la sesión principal o el `backend-developer`.
- Las decisiones de negocio y los puntos de control de los flujos se consultan siempre al usuario. Nunca se delegan.

## Herramientas
- Usa búsqueda web cuando un flujo pida referencias actuales (versiones de NuGet, Microsoft Learn), sobre todo en las sugerencias proactivas de `/project-init`.
- Corre `dotnet build` y `dotnet test` para verificar. No declares una tarea terminada sin compilar.
