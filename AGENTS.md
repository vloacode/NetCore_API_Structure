# AGENTS.md — Instrucciones para cualquier IA

Este repositorio usa el kit **NetCore_API_Structure**: una guía para construir **APIs ASP.NET Core (ecosistema Microsoft .NET, versión LTS vigente; hoy .NET 10)** siempre de la misma forma.
Este archivo es la **entrada universal**. Lo leen Claude Code, Cursor, Copilot, Codex y modelos locales. Síguelo antes de hacer cualquier cosa.

## 1. Orden de lectura obligatorio
1. Este archivo completo.
2. `docs/00-MASTER_CONTEXT.md`: resumen del proyecto y **Perfil del proyecto**. Si todavía contiene marcadores `{{...}}`, el proyecto no se ha inicializado: ejecuta el flujo `ai/workflows/project-init.md`.
3. `docs/ai/PROJECT_STATUS.md`: **fase actual** del plan y siguiente paso (`ai/implementation-phases.md`).
4. `ai/context-packs.md`: qué archivos mínimos leer según la tarea.
5. Solo los archivos que indique el context pack o la fase. **No cargues todo el repositorio.**

## 2. Stack (fijo, no negociable)
- .NET en su **versión LTS vigente** (por defecto .NET 10; `TargetFramework` del perfil, política en `standards/16-framework-versions.md`). ASP.NET Core Web API con **Controllers**, C# con `Nullable` habilitado.
- EF Core (misma versión mayor que .NET) con **SQL Server o PostgreSQL** (`Database` del perfil), migraciones con `dotnet-ef`.
- Repository + Unit of Work sobre EF Core, Specification pattern. Configuración en `Program.cs` + `Extensions/` (una extensión por área).
- FluentValidation, ProblemDetails (RFC 9457), OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Scalar, OpenTelemetry.
- Con seguridad (`Security = enabled`): ASP.NET Core Identity **local** + JWT propio + refresh tokens. Sin proveedores externos.
- Pruebas: xUnit. Contenedores: Docker. Despliegue opcional: Azure (App Service / Container Apps) o IIS.
- **El código se genera con las plantillas del kit**: `dotnet new kitapi` (solución) y `dotnet new kit-entity` (cada entidad). No se copia código desde los md.
- **Prohibido**: AutoMapper, MediatR (salvo ADR), lógica en controllers, `DateTime.Now`, SQL concatenado, secretos en `appsettings.json`.

## 3. Mapa del repositorio

| Carpeta | Qué es | ¿Se edita en un proyecto? |
|---|---|---|
| `standards/` | Reglas: arquitectura, persistencia, API, auth, entidades… (enlazan al código real). Índice: `standards/00-INDEX.md` | **No** (solo con `upgrade-kit`) |
| `templates/` | Plantillas `dotnet new`: `api/` (`kitapi`) y `entity/` (`kit-entity`). Fuente de verdad del código | **No** (solo con `upgrade-kit`) |
| `ai/implementation-phases.md` | Fases de construcción: qué leer y verificar en cada una | **No** |
| `ai/roles/` | Roles especializados (analista, arquitecto, backend, QA, seguridad...) | **No** |
| `ai/workflows/` | Flujos paso a paso (`project-init`, `new-entity`, `code-review`...) | **No** |
| `ai/context-packs.md` | Tarea → archivos mínimos a leer | **No** |
| `ai/suggestions-catalog.md` | Capacidades opcionales para sugerir, con referencias oficiales | **No** |
| `ai/references.md` | Documentación oficial por tema y cómo buscar en ella | **No** |
| `docs/` | Lo propio del proyecto: visión, requerimientos, dominio, módulos, decisiones, estado | **Sí**, siempre actualizado |
| `src/`, `tests/`, `{Project}.slnx` | Código de la solución (generado con `kitapi`) | Sí |
| `.claude/`, `.cursor/`, `.vscode/`, `.github/copilot-instructions.md`, `.mcp.json` | Adaptadores por herramienta y MCP de Microsoft Learn | No |

## 4. Cómo trabajar una tarea
1. **Clasifica** la tarea (inicializar proyecto, nuevo módulo, nueva entidad, endpoint, cambio de BD, revisión, incidente...).
2. Abre el **flujo** correspondiente en `ai/workflows/` y síguelo paso a paso. Si ningún flujo encaja, usa `new-feature.md`.
3. Carga el **context pack** de esa tarea (`ai/context-packs.md`).
4. Adopta el **rol** que indique el flujo (`ai/roles/`). Si tu herramienta tiene subagentes, delega en ellos.
5. Implementa respetando los standards. Ante una duda de negocio, **pregunta**; no inventes. Ante una duda técnica (API, versión, rendimiento), **consulta la documentación oficial** (`ai/references.md`; MCP de Microsoft Learn si está conectado) en lugar de responder de memoria.
6. Cumple la **Definition of Done** (sección 7) antes de dar la tarea por terminada.

Flujos disponibles: `project-init`, `new-module`, `new-entity`, `new-feature`, `api-endpoint`, `database-change`, `test-generation`, `code-review`, `security-review`, `performance-review`, `log-analysis`, `incident-response`, `record-decision`, `docs-sync`, `upgrade-kit`, `upgrade-dotnet`, `analytics-event` (si `product-analytics` está activa).

## 5. Reglas de oro
Resumen; el detalle está en `standards/01-solution-architecture.md`.
1. Controllers → Services → `IUnitOfWork`/`IRepository` → `DbContext`. Nunca saltarse capas.
2. Los services devuelven `Result`/`Result<T>`. Los errores se declaran en `{Entity}Errors`.
3. Por la API solo viajan DTOs (`{Entity}Dto`, `Create/Update{Entity}Request`), nunca entidades EF.
4. Las consultas con filtro, orden o paginación usan una `Specification<T>`. Prohibido paginar sin orden.
5. La auditoría y el soft delete son automáticos (interceptor). No asignarlos a mano.
6. Las escrituras de varios pasos van en `uow.ExecuteInTransactionAsync`, sin efectos externos dentro.
7. `CancellationToken` en todo método async. Fechas UTC con `TimeProvider`.
8. Con seguridad: cada endpoint lleva `[HasPermission(...)]` y cada permiso nuevo se registra en `Permissions`.
9. **Nunca crear entidades de ejemplo.** Las entidades salen de `docs/05-domain-model.md` y `docs/modules/`, y se generan con `kit-entity` en `Features/{Entities}/`.
9b. Construir **por fases** (`ai/implementation-phases.md`): una fase o una entidad por sesión, con su verificación en verde.
10. Si algo contradice un standard, se registra un ADR (`ai/workflows/record-decision.md`) antes de hacerlo.

## 6. Idioma y estilo
- Comunicación con el usuario y documentación en **español**.
- Identificadores de código (clases, métodos, rutas, tablas) en **inglés**. Comentarios en español, breves y solo cuando aportan.
- Mensajes de error para el usuario final en español, dentro de `{Entity}Errors`.

## 7. Definition of Done (toda tarea con código)
- [ ] `dotnet format` aplicado y `dotnet build` sin errores ni warnings nuevos.
- [ ] Tests nuevos o actualizados, y `dotnet test` en verde.
- [ ] Migración creada si cambió el modelo, con el SQL generado revisado.
- [ ] Permisos registrados y aplicados (perfil con seguridad).
- [ ] `docs/` actualizado: módulo, índice de requerimientos y permisos si aplica (`ai/workflows/docs-sync.md`).
- [ ] `docs/ai/PROJECT_STATUS.md` actualizado.
- [ ] Lecciones o trampas nuevas anotadas en `docs/ai/AI_MEMORY.md`.

## 8. Comandos frecuentes
```bash
dotnet build
dotnet test
dotnet run --project src/{Project}.Api
dotnet ef migrations add <Nombre> -p src/{Project}.Api -o Infrastructure/Persistence/Migrations
dotnet ef database update -p src/{Project}.Api
dotnet user-secrets set "<Clave>" "<Valor>" --project src/{Project}.Api
```

## 9. Verificación del kit
- `python scripts/check-kit.py` comprueba enlaces, que cada flujo y cada rol tengan su adaptador, y el tamaño de los archivos. Se ejecuta en CI (`.github/workflows/kit-checks.yml`).
- `python scripts/verify/verify-template.py` genera las plantillas en la matriz perfil × motor (con `kit-entity`, migración, `format` y build Release) y, con `--run-tests` / `--smoke-sqlserver` / `--smoke-postgresql`, las prueba contra la base real. Úsalo al **modificar el kit** (ver `scripts/verify/README.md`).
- Claude Code aplica además hooks automáticos (`.claude/settings.json`). Con otras IAs, cumplir lo mismo a mano: nada de secretos en `appsettings*.json`, no editar `standards/` ni `ai/` en un proyecto y no terminar sin `dotnet build`.

## 10. Para modelos con contexto limitado (IA local)
- Lee **solo** el context pack de la tarea o la fase actual. Cada archivo de `ai/` y `standards/` se entiende por sí solo.
- Sigue `ai/implementation-phases.md`: una fase o una entidad por sesión, guardando el avance en `PROJECT_STATUS.md`. Compila entre pasos.
- Genera con las plantillas en lugar de escribir código largo: menos tokens y menos errores.
- Si no tienes acceso web, usa `ai/references.md` y `ai/suggestions-catalog.md` como referencia y marca las versiones de paquetes como "verificar".
