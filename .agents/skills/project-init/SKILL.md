---
name: project-init
description: "Inicializa un proyecto nuevo de API .NET desde el kit: elige el modo (con seguridad, sin seguridad o entrevista completa), entrevista, sugiere capacidades con referencias, llena docs/ y crea la solución. Úsalo cuando docs/00-MASTER_CONTEXT.md tenga marcadores {{...}} o el usuario pida crear o empezar el proyecto."
---

# project-init

Flujo **project-init** del kit NetCore_API_Structure (APIs ASP.NET Core).

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` (en la raíz del proyecto) y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/project-init.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`; trabaja por fases (`ai/implementation-phases.md`).
4. Cuando el flujo nombre un rol, adopta las reglas de `ai/roles/<rol>.md` (o delega en el subagente de ese rol si tu herramienta los tiene).
5. Las decisiones de negocio se consultan siempre al usuario. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Todas las rutas son relativas a la raíz del proyecto (donde está `AGENTS.md`).
