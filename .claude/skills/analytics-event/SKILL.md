---
name: analytics-event
description: Agrega un evento de analítica de uso de punta a punta (pregunta de negocio y KPI, definición sin datos personales, constante, Track después del guardado, test y consulta). Úsalo cuando se quiera medir algo del uso del producto y la capacidad product-analytics esté activa.
---

# /analytics-event

Este comando ejecuta el flujo **analytics-event** del kit NetCore_API_Structure.

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md` (debe incluir `product-analytics` en `Capabilities`).
2. Lee **completo** `ai/workflows/analytics-event.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`.
4. Delega en los subagentes de `.claude/agents/` que el flujo indique para cada rol. Las decisiones de negocio se consultan siempre al usuario.
5. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Contexto adicional del usuario (si lo dio al invocar el comando): $ARGUMENTS
