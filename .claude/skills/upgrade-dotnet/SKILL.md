---
name: upgrade-dotnet
description: Migra un proyecto existente a otra versión de .NET (por ejemplo a la nueva LTS) de forma controlada - investiga breaking changes con la documentación oficial, actualiza TargetFramework, paquetes, Docker y CI, compila, prueba y documenta.
---

# /upgrade-dotnet

Este comando ejecuta el flujo **upgrade-dotnet** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/upgrade-dotnet.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`. Para los breaking changes, usa el MCP de Microsoft Learn o las fuentes de `ai/references.md`.
4. Delega en los subagentes de `.claude/agents/` que el flujo indique para cada rol. Las decisiones se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario (si lo dio al invocar el comando): $ARGUMENTS
