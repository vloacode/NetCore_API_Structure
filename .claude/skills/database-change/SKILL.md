---
name: database-change
description: Cambia el esquema de datos con una migración EF Core segura y revisada (columnas, tipos, índices, relaciones), con plan en dos pasos si es destructivo.
---

# /database-change

Este comando ejecuta el flujo **database-change** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/database-change.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`.
4. Delega en los subagentes de `.claude/agents/` que el flujo indique para cada rol. Las decisiones de negocio se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario (si lo dio al invocar el comando): $ARGUMENTS
