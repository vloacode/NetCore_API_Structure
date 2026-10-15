---
description: "Migra un proyecto existente a otra versión de .NET (por ejemplo a la nueva LTS) de forma controlada - investiga breaking changes con la documentación oficial, actualiza TargetFramework, paquetes, Docker y CI, compila, prueba y documenta."
---
Ejecuta el flujo **upgrade-dotnet** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/upgrade-dotnet.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`.
4. Delega en los subagentes de `.opencode/agents/` que el flujo indique para cada rol. Las decisiones de negocio se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario: $ARGUMENTS
