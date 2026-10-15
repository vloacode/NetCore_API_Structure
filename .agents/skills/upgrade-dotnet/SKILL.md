---
name: upgrade-dotnet
description: "Migra un proyecto existente a otra versión de .NET (por ejemplo a la nueva LTS) de forma controlada - investiga breaking changes con la documentación oficial, actualiza TargetFramework, paquetes, Docker y CI, compila, prueba y documenta."
---

# upgrade-dotnet

Flujo **upgrade-dotnet** del kit NetCore_API_Structure (APIs ASP.NET Core).

1. Si aún no lo leíste en esta sesión, lee `AGENTS.md` (en la raíz del proyecto) y el Perfil del proyecto en `docs/00-MASTER_CONTEXT.md`.
2. Lee **completo** `ai/workflows/upgrade-dotnet.md` y síguelo paso a paso, incluidos sus puntos de control con el usuario.
3. Carga solo los archivos que indican el flujo y `ai/context-packs.md`; trabaja por fases (`ai/implementation-phases.md`).
4. Cuando el flujo nombre un rol, adopta las reglas de `ai/roles/<rol>.md` (o delega en el subagente de ese rol si tu herramienta los tiene).
5. Las decisiones de negocio se consultan siempre al usuario. Antes de terminar, cumple la Definition of Done del flujo y la de `AGENTS.md`.

Todas las rutas son relativas a la raíz del proyecto (donde está `AGENTS.md`).
