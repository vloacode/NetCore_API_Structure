# Herramientas de IA y modelos locales

El kit funciona con cualquier IA que lea `AGENTS.md`. Para que cada herramienta detecte también los **flujos** (skills / comandos), los **roles** (subagentes) y el **MCP de Microsoft Learn**, el repositorio trae un adaptador por herramienta. Todos apuntan a la misma fuente: `ai/workflows/` y `ai/roles/`.

## Qué lee cada herramienta

| Herramienta | Instrucciones | Flujos | Roles (subagentes) | MCP Microsoft Learn | Configuración extra |
|---|---|---|---|---|---|
| **Claude Code** | `CLAUDE.md` → `AGENTS.md` | `.claude/skills/` (`/project-init`…) | `.claude/agents/` | `.mcp.json` | Ninguna (hooks en `.claude/settings.json`) |
| **opencode** | `AGENTS.md` | `.opencode/commands/` (`/project-init`…) + skills de `.agents/skills/` y `.claude/skills/` | `.opencode/agents/` | `opencode.json` | Ninguna |
| **Hermes Agent** | `AGENTS.md` | `.agents/skills/` (cada skill es un `/comando`) | Usa los roles de `ai/roles/` según el flujo | `~/.hermes/config.yaml` (abajo) | Una vez por proyecto: `hermes skills trust` |
| **Cursor** | `.cursor/rules/kit.mdc` → `AGENTS.md` | Pedirlos por nombre ("sigue ai/workflows/new-entity.md") | `ai/roles/` | `.cursor/mcp.json` | Ninguna |
| **GitHub Copilot** | `.github/copilot-instructions.md` → `AGENTS.md` | Pedirlos por nombre | `ai/roles/` | `.vscode/mcp.json` | Ninguna |
| **Codex / otras** | `AGENTS.md` | `.agents/skills/` si la herramienta usa el estándar agentskills.io; si no, pedirlos por nombre | `ai/roles/` | Según la herramienta | — |

> **No crear `.hermes.md`** en el proyecto: Hermes lo prioriza sobre `AGENTS.md` y dejaría de leer las instrucciones del kit.

### Hermes Agent
1. En la raíz del proyecto, una sola vez: `hermes skills trust` (Hermes no carga skills de un repo sin esta confirmación).
2. MCP de Microsoft Learn (opcional, recomendado), en `~/.hermes/config.yaml`:
   ```yaml
   mcp_servers:
     microsoft-learn:
       url: "https://learn.microsoft.com/api/mcp"
   ```
   Probarlo: `hermes mcp test microsoft-learn`. Si responde 400/405, agregar `transport: sse`.

### opencode
Todo viene en el repositorio: `AGENTS.md`, `opencode.json` (MCP), `.opencode/commands/` y `.opencode/agents/`. Si se definió `OPENCODE_DISABLE_CLAUDE_CODE=1`, igual funciona: las skills también están en `.agents/skills/`.

## Modelos locales o privados (Ollama, llama.cpp, LM Studio, vLLM)
Que la herramienta encuentre los archivos es la mitad; la otra mitad es cómo se sirve el modelo.

1. **Contexto de 32K tokens o más.** Con menos, el servidor corta el system prompt y la IA "no ve" `AGENTS.md` ni la lista de skills.
   - Ollama: `OLLAMA_CONTEXT_LENGTH=32768` (variable de entorno del servidor) o `PARAMETER num_ctx 32768` en un Modelfile.
   - llama.cpp: `-c 32768`.
   - LM Studio: *Context Length* del modelo en 32768 o más.
2. **Tool calling.** Skills, subagentes, lectura de archivos y terminal son herramientas: el modelo y el servidor deben soportar *function calling* con su plantilla de chat.
   - llama.cpp: iniciar con `--jinja`.
   - Ollama: usar una variante del modelo marcada con *tools*.
   - vLLM: `--enable-auto-tool-choice` y el `--tool-call-parser` del modelo.
3. **Trabajar por fases.** Una fase o una entidad por sesión (`ai/implementation-phases.md`), cargando solo el context pack de la tarea (`ai/context-packs.md`). El estado queda en `docs/ai/PROJECT_STATUS.md` para retomar en otra sesión.
4. **Generar en lugar de escribir.** `dnx NetCoreApiKit` y `dotnet new kit-entity` producen el código; el modelo solo adapta. Menos tokens y menos errores.
5. **Sin acceso web**: usar `ai/references.md` y marcar las versiones de paquetes como "verificar".

## Mantener los adaptadores
Los adaptadores de `.agents/`, `.opencode/commands/` y `.opencode/agents/` se generan con `python scripts/sync-adapters.py` a partir de `ai/workflows/`, `ai/roles/` y las descripciones de `.claude/`. `scripts/check-kit.py` falla si están desactualizados.
