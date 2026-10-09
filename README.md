# NetCore_API_Structure

Kit para que una IA (Claude Code, Cursor, Copilot, Codex o un modelo local) construya **APIs ASP.NET Core** (versión LTS vigente de .NET, hoy .NET 10) con **SQL Server o PostgreSQL**, siempre con la misma arquitectura, de principio a fin: desde la entrevista inicial hasta el código, las pruebas y la documentación.

> Versión **3.0.0** (ver `CHANGELOG.md`). El código vive en plantillas `dotnet new` (`templates/`) verificadas en la matriz perfil (con/sin seguridad) × motor (SQL Server/PostgreSQL): generan, compilan en Release con analizadores, crean migraciones, pasan los tests de integración con Testcontainers y las pruebas de punta a punta contra bases reales (`scripts/verify/`).

**Stack:** Controllers · services con `Result<T>` · EF Core con Repository + Unit of Work + Specification · FluentValidation · `Program.cs` como índice con `Extensions/` por área · OpenTelemetry · Identity local + JWT (opcional). Sin MediatR ni AutoMapper.

## Qué incluye

| Parte | Para qué sirve |
|---|---|
| `AGENTS.md` | Entrada universal para cualquier IA: stack, reglas, mapa y Definition of Done |
| `CLAUDE.md` | Lo mismo para Claude Code, más cómo usar sus comandos y subagentes |
| `templates/` | Plantillas `dotnet new`: `kitapi` (la solución completa según el perfil) y `kit-entity` (cada entidad en `Features/{Entities}/`, 4 archivos + test). Son la fuente de verdad del código |
| `standards/` | Las reglas: arquitectura, persistencia, API, autenticación, entidades, testing y despliegue. Cortos y enlazados al código real |
| `ai/implementation-phases.md` | Construcción por fases: qué leer, producir y verificar en cada una, para no cargar todo el kit de una vez |
| `ai/workflows/` | Flujos paso a paso: `project-init`, `new-entity`, `code-review`, `incident-response`… |
| `ai/roles/` | Roles especializados: analista, arquitecto, backend, QA, seguridad, DevOps… |
| `ai/context-packs.md` | Qué archivos leer para cada tarea (ahorra contexto) |
| `ai/suggestions-catalog.md` | Capacidades opcionales que la IA sugiere, con referencias oficiales |
| `ai/references.md` + `.mcp.json` | Documentación oficial para consultar (y MCP de Microsoft Learn para buscarla en vivo) |
| `docs/` | Plantillas de la documentación del proyecto, que llena `/project-init` |
| Capacidades opcionales | Analítica de uso (`standards/17`), Nginx (`standards/13a`), API key y las del catálogo de sugerencias |

## Modos de creación
`project-init` empieza siempre preguntando el modo:

1. **Estándar con seguridad**: arquitectura completa + Identity local + JWT + refresh tokens + 2FA + usuarios, roles y permisos.
2. **Estándar sin seguridad**: lo mismo sin autenticación, para APIs públicas. Mantiene rate limiting, validación, CORS y HTTPS.
3. **Entrevista completa**: la IA pregunta por negocio, seguridad, datos, integraciones y despliegue, y ajusta todo a las respuestas.

En los tres modos la IA sugiere de forma proactiva capacidades adicionales (versionado, caché, archivos, jobs…) con referencias actuales.

## Cómo crear un proyecto nuevo
1. En GitHub, pulsa **Use this template** → **Create a new repository** (o copia el contenido de este repositorio).
2. Abre el repositorio nuevo con tu IA.
3. Pídele que inicialice el proyecto:
   - Claude Code: `/project-init`
   - Otras IAs: "Lee AGENTS.md y ejecuta ai/workflows/project-init.md".
4. Responde la elección de modo y la entrevista, revisa los documentos que genera y aprueba la creación del código.
5. La IA genera la solución con `dotnet new kitapi` y luego avanza **por fases** (una entidad o una capacidad por sesión), dejando el estado en `docs/ai/PROJECT_STATUS.md`.

Sin IA también se puede usar directamente:
```bash
dotnet new install ./templates/api
dotnet new install ./templates/entity
dotnet new kitapi -n MiApi --security true --database postgresql
cd MiApi
dotnet new kit-entity -n Customer --app MiApi --security true --database postgresql
```

## Cómo seguir trabajando
- Nueva entidad: `/new-entity` (o "sigue ai/workflows/new-entity.md"), que usa `dotnet new kit-entity`.
- Nuevo módulo, endpoint, cambio de BD, revisiones, incidentes: ver la lista de flujos en `AGENTS.md`.

## Actualizar un proyecto existente al kit nuevo
Compara `KIT_VERSION` del proyecto con el de este repositorio y ejecuta `/upgrade-kit`. Solo se actualizan `standards/`, `templates/`, `ai/` y los adaptadores; `docs/` y `src/` del proyecto no se tocan.
