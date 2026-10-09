# Flujo: project-init (inicializar un proyecto)

## Objetivo
Llevar un repositorio recién creado desde el kit hasta una **API .NET funcionando**: documentación del proyecto llena, decisiones registradas, plan de fases y solución generada con `dotnet new kitapi` según el perfil, compilando y con tests. Las entidades se agregan después, fase por fase.

## Cuándo usarlo
- `docs/00-MASTER_CONTEXT.md` todavía tiene marcadores `{{...}}`.
- El usuario pide "crear el proyecto", "inicializar" o "empezar".

## Roles
Orquesta la sesión principal. Delega en **solution-architect** (perfil, sugerencias, ADR), **product-analyst** (negocio y docs), **backend-developer** (código) y **qa-engineer** (tests).

## Archivos a leer
Pasos 1–6 (fase 0): `AGENTS.md`, `ai/implementation-phases.md`, `ai/suggestions-catalog.md` y las plantillas de `docs/`. Paso 7 (fase 1): `standards/01-solution-architecture.md`. No leer otros standards en esta sesión.

---

## Paso 1 — Detectar el estado
- Si `src/` ya tiene código o `docs/00-MASTER_CONTEXT.md` ya está lleno, **detenerse**: es un proyecto existente. Ofrecer `upgrade-kit` (si el kit es más nuevo) o un inventario del estado. No reinicializar.
- Si el usuario adjunta un documento (brief, requerimientos, acta), leerlo completo: servirá para responder la entrevista sin preguntar lo que ya está escrito.

## Paso 2 — Elegir el modo (SIEMPRE lo primero, antes de cualquier otra pregunta)
Presentar exactamente estas tres opciones:

> **¿Cómo quieres crear el proyecto?**
> 1. **Estándar con seguridad**: arquitectura completa + Identity local + JWT + refresh tokens + 2FA + usuarios, roles y permisos. Solo te pregunto lo mínimo.
> 2. **Estándar sin seguridad (API pública)**: todo lo anterior sin autenticación. Mantiene HTTPS, rate limiting, validación, CORS y headers de seguridad. Solo te pregunto lo mínimo.
> 3. **Entrevista completa**: te pregunto por negocio, seguridad, datos, capacidades e infraestructura, y ajusto todo a tus respuestas.

| Modo | Perfil inicial |
|---|---|
| 1 | `Mode=1-standard-secure`, `Security=enabled`, `TwoFactor=enabled`, `PublicRegistration=enabled`, `ApiKey=disabled`, `PrimaryKey=int`, `SoftDelete=enabled`, `Database=sqlserver` (o el elegido), `TargetFramework=` LTS vigente, `Email=log`, `Deployment=docker` |
| 2 | `Mode=2-standard-public`, `Security=disabled`, `TwoFactor=disabled`, `PublicRegistration=disabled`, `ApiKey=disabled` (se pregunta), `PrimaryKey=int`, `SoftDelete=enabled`, `Database=sqlserver` (o el elegido), `TargetFramework=` LTS vigente, `Email=log`, `Deployment=docker` |
| 3 | Parte del modo 1 y cada valor se ajusta con la entrevista |

## Paso 3 — Recoger información
Preguntar en **rondas cortas** (3 a 6 preguntas), con opciones cuando se pueda. No preguntar lo que ya respondió un documento adjunto.

### Mínimo (modos 1 y 2)
1. Nombre del proyecto (`{Project}`, PascalCase, sin espacios).
2. En pocas líneas, qué hace la API y quién la consume.
3. Módulos y entidades principales: para cada entidad, sus campos importantes, cuáles son obligatorios o únicos, y sus relaciones.
4. Origen del frontend para CORS (por ejemplo `https://localhost:5173`).
5. Base de datos: **SQL Server** (por defecto) o **PostgreSQL**.
6. Versión de .NET: por defecto la **LTS vigente**. Si hay acceso web, confirmarla en la política oficial (`standards/16`) antes de proponerla.
7. Solo modo 2: ¿las operaciones de escritura (crear, editar, eliminar) serán públicas, protegidas con API key o no existirán?

### Entrevista completa (modo 3), además del mínimo
- **Ronda A — Negocio:** problema y objetivos; actores; módulos; reglas de negocio importantes; estados y flujos; qué queda fuera de alcance.
- **Ronda B — Seguridad:** ¿hay usuarios? (si no, `Security=disabled`); ¿registro público o solo los crea un admin?; ¿2FA?; roles además de Admin y User, y qué puede hacer cada uno; ¿hay datos que solo puede ver su dueño?; ¿API key para sistemas externos?
- **Ronda C — Datos:** motor (SQL Server o PostgreSQL) y versión del motor; ¿int o Guid como PK?; ¿soft delete?; ¿historial de cambios (auditoría detallada)?; volumen esperado; datos sensibles y retención.
- **Ronda D — Capacidades e integraciones:** emails reales; archivos; tareas programadas; integraciones con terceros; tiempo real; caché; versionado; multi-empresa; idiomas; analítica de uso ("¿qué quieren saber de cómo se usa el producto?" → `product-analytics`). Usar los disparadores de `ai/suggestions-catalog.md` para guiar las preguntas.
- **Ronda E — Infraestructura:** dónde se despliega (Docker, servidor Linux/VM con Nginx, Azure App Service, Container Apps, IIS); entornos; destino de logs; objetivos de rendimiento y disponibilidad (si no los sabe, proponer los valores por defecto de `docs/04`).

## Paso 4 — Sugerencias proactivas (todos los modos)
Con todo lo conversado, actuar como **solution-architect**:
1. Recorrer los disparadores de `ai/suggestions-catalog.md` y elegir las capacidades que encajan **con este proyecto**. Pensar también en lo que no está en el catálogo: riesgos, normativa del sector, casos borde.
2. **Si hay acceso web**, buscar referencias actuales en las fuentes de `ai/references.md` (o con el MCP de Microsoft Learn): documentación oficial, versión vigente en nuget.org y buenas prácticas recientes para el tipo de sistema. Dar el enlace de cada referencia.
3. **Si no hay acceso web** (por ejemplo, un modelo local), usar los enlaces del catálogo y marcar las versiones como "verificar versión".
4. Presentar las sugerencias en una tabla: capacidad, por qué aquí, costo o complejidad, referencia. El usuario **acepta, pospone o descarta** cada una.
5. Ajustar el perfil (`Capabilities`) con lo aceptado.

## Paso 5 — Llenar la documentación (punto de control 1)
Delegar en **product-analyst** y **solution-architect**:
1. `docs/00-MASTER_CONTEXT.md`: Perfil completo, resumen, actores, módulos.
2. `docs/01` a `10`: llenar con lo conversado. Lo que no se sepa: "Por definir" + pregunta abierta en `docs/10-roadmap.md`. Sin seguridad: `07-permissions-matrix.md` solo con la sección "Perfil público".
3. Un `docs/modules/<modulo>.md` por módulo, desde `_TEMPLATE.md`, con requerimientos funcionales, entidades, reglas, endpoints y errores.
4. `docs/decisions/ADR-0001-decisiones-iniciales.md`: modo, perfil, sugerencias aceptadas, pospuestas y descartadas, con su motivo.
5. Verificar que no quedan `{{` en los documentos llenados.
6. **Mostrar al usuario un resumen** (perfil, módulos, entidades, sugerencias aceptadas) y **esperar su aprobación** antes de escribir código. Si pide cambios, ajustar y volver a mostrar.

## Paso 6 — Plan de fases
Escribir en `docs/ai/PROJECT_STATUS.md` el plan concreto (`ai/implementation-phases.md`): módulos en orden, entidades de cada módulo en orden de dependencia (padres primero) y capacidades aceptadas. Desde aquí, **cada fase se trabaja por separado** y solo con los archivos que indica.

## Paso 7 — Fase 1: esqueleto (punto de control 2)
Delegar en **backend-developer**:
1. Instalar las plantillas (una vez por máquina) y generar la solución **en la raíz del repositorio**, con las opciones del perfil:
   ```bash
   dotnet new install ./templates/api
   dotnet new install ./templates/entity
   dotnet new kitapi -n {Project} -o . --force --security <true|false> --database <sqlserver|postgresql> --analytics <true|false> --apikey <true|false> --framework <TargetFramework>
   ```
   `--force` solo reemplaza el `.gitignore` del kit por el del proyecto (que ya ignora `bin/`, `obj/`, secretos y `.env`).
2. Verificar que se crearon `{Project}.slnx`, `src/{Project}.Api/` y `tests/{Project}.IntegrationTests/`, y que `docs/`, `ai/`, `standards/` y `AGENTS.md` siguen en su lugar.
3. User-secrets (llave JWT, password del admin, `Analytics:HashKey` según el perfil) y orígenes de CORS en `appsettings.json`.
4. `dotnet ef migrations add InitialCreate -p src/{Project}.Api -o Infrastructure/Persistence/Migrations`.
5. `dotnet build -c Release` y `dotnet test` **en verde** (los tests usan Testcontainers: requieren Docker; si no hay, avisar y dejar los tests listos).
6. Actualizar `PROJECT_STATUS.md` (fase 1 ✅) y proponer commit.

## Paso 8 — Fases siguientes
Seguir el plan de `PROJECT_STATUS.md` con `ai/implementation-phases.md`:
- **Fase 2**: por cada entidad, el flujo `ai/workflows/new-entity.md` (una por sesión si el contexto es limitado).
- **Fase 3**: cada capacidad aceptada, una a la vez.
- **Fases 4 y 5**: endurecimiento y despliegue.

## Paso 9 — Cierre de la inicialización
Al terminar la fase 1 (y cada fase posterior), resumen al usuario:
- Perfil y capacidades activas.
- Qué se creó y cómo correrlo: `dotnet run --project src/{Project}.Api`, `/scalar` y, con seguridad, el admin del seed (contraseña en user-secrets).
- Siguiente fase del plan y preguntas abiertas.

---

## Reglas
- **Nunca** escribir código antes del punto de control 1 aprobado.
- **Nunca** crear entidades que el usuario no definió, ni entidades de ejemplo.
- Ante una respuesta ambigua, preguntar. Si se supone algo para avanzar, marcarlo "(supuesto, confirmar)".
- Las decisiones las toma el usuario; la IA propone con opciones y una recomendación.
- Trabajar por fases (`ai/implementation-phases.md`): una fase o un ítem por sesión, guardando el avance en `PROJECT_STATUS.md`.

## Salida esperada
Documentación del proyecto llena, ADR-0001, plan de fases en `PROJECT_STATUS.md` y solución generada compilando, con migración inicial y tests en verde.

## Definition of Done
- [ ] Sin marcadores `{{` en `docs/` (salvo plantillas `_TEMPLATE` y `ADR-0000`).
- [ ] Perfil completo y coherente con lo implementado.
- [ ] `dotnet build` y `dotnet test` en verde (o tests listos si no hay Docker, informado al usuario).
- [ ] Migración `InitialCreate` creada.
- [ ] Plan de fases escrito en `PROJECT_STATUS.md`.
- [ ] `PROJECT_STATUS.md` y `00-MASTER_CONTEXT.md` actualizados.
