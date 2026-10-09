---
name: project-init
description: Inicializa un proyecto nuevo de API .NET desde el kit: elige el modo (con seguridad, sin seguridad o entrevista completa), entrevista, sugiere capacidades con referencias, llena docs/ y crea la solución. Úsalo cuando docs/00-MASTER_CONTEXT.md tenga marcadores {{...}} o el usuario pida crear o empezar el proyecto.
---

# /project-init

Este comando ejecuta el flujo **project-init** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/project-init.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`.
4. Delega en los subagentes de `.claude/agents/` que el flujo indique para cada rol. Las decisiones de negocio se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario (si lo dio al invocar el comando): $ARGUMENTS
