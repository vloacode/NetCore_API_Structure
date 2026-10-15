---
name: analytics-event
description: "Agrega un evento de analítica de uso de punta a punta (pregunta de negocio y KPI, definición sin datos personales, constante, Track después del guardado, test y consulta). Úsalo cuando se quiera medir algo del uso del producto y la capacidad product-analytics esté activa."
---

# analytics-event

Flujo **analytics-event** del kit NetCore_API_Structure (APIs ASP.NET Core).

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` (en la raíz del proyecto) y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/analytics-event.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`; trabaja por fases (`ai/implementation-phases.md`).
4. Cuando el flujo nombre un rol, adopta las reglas de `ai/roles/<rol>.md` (o delega en el subagente de ese rol si tu herramienta los tiene).
5. Las decisiones de negocio se consultan siempre al usuario. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Todas las rutas son relativas a la raíz del proyecto (donde está `AGENTS.md`).
