# AGENTS.md — Instrucciones para cualquier IA

Este repositorio usa el kit **NetCore_API_Structure**: una guía para construir **APIs ASP.NET Core (.NET 10, ecosistema Microsoft)** siempre de la misma forma.
Este archivo es la **entrada universal**. Lo leen Claude Code, Cursor, Copilot, Codex y modelos locales. Síguelo antes de hacer cualquier cosa.

## 1. Orden de lectura obligatorio
1. Este archivo completo.
2. `docs/00-MASTER_CONTEXT.md`: resumen del proyecto y **Perfil del proyecto**. Si no está lleno, el proyecto no se ha inicializado: ejecuta el flujo `ai/workflows/project-init.md`.
3. `ai/context-packs.md`: qué archivos mínimos leer según la tarea.
4. Solo los archivos que indique el context pack. **No cargues todo el repositorio.**

## 2. Stack (fijo, no negociable)
- .NET 10 (LTS), ASP.NET Core Web API con **Controllers**, C# con `Nullable` habilitado.
- EF Core 10 + SQL Server, migraciones con `dotnet-ef`.
- FluentValidation, ProblemDetails (RFC 9457), OpenAPI (`Microsoft.AspNetCore.OpenApi`) + Scalar.
- Con seguridad (`Security = enabled`): ASP.NET Core Identity **local** + JWT propio + refresh tokens. Sin proveedores externos.
- Pruebas: xUnit. Contenedores: Docker. Despliegue opcional: Azure (App Service / Container Apps) o IIS.
- **Prohibido**: AutoMapper, MediatR (salvo ADR), lógica en controllers, `DateTime.Now`, SQL concatenado, secretos en `appsettings.json`.

## 3. Mapa del repositorio

| Carpeta | Qué es | ¿Se edita en un proyecto? |
|---|---|---|
| `standards/` | Cómo se construye: arquitectura, persistencia, API, auth, plantillas. Índice: `standards/00-INDEX.md` | **No** (solo con `upgrade-kit`) |
| `ai/roles/` | Roles especializados (analista, arquitecto, backend, QA, seguridad...) | **No** |
| `ai/workflows/` | Flujos paso a paso (`project-init`, `new-entity`, `code-review`...) | **No** |
| `ai/context-packs.md` | Tarea → archivos mínimos a leer | **No** |
| `ai/suggestions-catalog.md` | Capacidades opcionales para sugerir, con referencias oficiales | **No** |
| `docs/` | Lo propio del proyecto: visión, requerimientos, dominio, módulos, decisiones, estado | **Sí**, siempre actualizado |
| `src/` | Código de la solución | Sí |
| `.claude/`, `.cursor/`, `.github/copilot-instructions.md` | Adaptadores por herramienta; apuntan a `ai/` y a este archivo | No |

## 4. Cómo trabajar una tarea
1. **Clasifica** la tarea (inicializar proyecto, nuevo módulo, nueva entidad, endpoint, cambio de BD, revisión, incidente...).
2. Abre el **flujo** correspondiente en `ai/workflows/` y síguelo paso a paso. Si ningún flujo encaja, usa `new-feature.md`.
3. Carga el **context pack** de esa tarea (`ai/context-packs.md`).
4. Adopta el **rol** que indique el flujo (`ai/roles/`). Si tu herramienta tiene subagentes, delega en ellos.
5. Implementa respetando los standards. Ante una duda de negocio, **pregunta**; no inventes.
6. Cumple la **Definition of Done** (sección 7) antes de dar la tarea por terminada.

Flujos disponibles: `project-init`, `new-module`, `new-entity`, `new-feature`, `api-endpoint`, `database-change`, `test-generation`, `code-review`, `security-review`, `performance-review`, `log-analysis`, `incident-response`, `record-decision`, `docs-sync`, `upgrade-kit`.

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
9. **Nunca crear entidades de ejemplo.** Las entidades salen de `docs/05-domain-model.md` y `docs/modules/`.
10. Si algo contradice un standard, se registra un ADR (`ai/workflows/record-decision.md`) antes de hacerlo.

## 6. Idioma y estilo
- Comunicación con el usuario y documentación en **español**.
- Identificadores de código (clases, métodos, rutas, tablas) en **inglés**. Comentarios en español, breves y solo cuando aportan.
- Mensajes de error para el usuario final en español, dentro de `{Entity}Errors`.

## 7. Definition of Done (toda tarea con código)
- [ ] `dotnet build` sin errores ni warnings nuevos.
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

## 9. Para modelos con contexto limitado (IA local)
- Lee **solo** el context pack de la tarea. Cada archivo de `ai/` y `standards/` se entiende por sí solo.
- Trabaja un archivo o una entidad por vez y compila entre pasos.
- Si no tienes acceso web, usa `ai/suggestions-catalog.md` como referencia y marca las versiones de paquetes como "verificar".
