# Flujo: project-init (inicializar un proyecto)

## Objetivo
Llevar un repositorio recién creado desde el kit hasta una **API .NET 10 funcionando**: documentación del proyecto llena, decisiones registradas, solución creada según el perfil, entidades implementadas, compilando y con tests.

## Cuándo usarlo
- `docs/00-MASTER_CONTEXT.md` todavía tiene marcadores `{{...}}`.
- El usuario pide "crear el proyecto", "inicializar" o "empezar".

## Roles
Orquesta la sesión principal. Delega en **solution-architect** (perfil, sugerencias, ADR), **product-analyst** (negocio y docs), **backend-developer** (código) y **qa-engineer** (tests).

## Archivos a leer
`AGENTS.md`, `ai/context-packs.md`, `ai/suggestions-catalog.md`, `standards/00-INDEX.md`, `standards/01-solution-architecture.md`, `standards/01a-bootstrap.md`, `standards/14-coding-standards.md` y las plantillas de `docs/`.

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
| 1 | `Mode=1-standard-secure`, `Security=enabled`, `TwoFactor=enabled`, `PublicRegistration=enabled`, `ApiKey=disabled`, `PrimaryKey=int`, `SoftDelete=enabled`, `Email=log`, `Deployment=docker` |
| 2 | `Mode=2-standard-public`, `Security=disabled`, `TwoFactor=disabled`, `PublicRegistration=disabled`, `ApiKey=disabled` (se pregunta), `PrimaryKey=int`, `SoftDelete=enabled`, `Email=log`, `Deployment=docker` |
| 3 | Parte del modo 1 y cada valor se ajusta con la entrevista |

## Paso 3 — Recoger información
Preguntar en **rondas cortas** (3 a 6 preguntas), con opciones cuando se pueda. No preguntar lo que ya respondió un documento adjunto.

### Mínimo (modos 1 y 2)
1. Nombre del proyecto (`{Project}`, PascalCase, sin espacios).
2. En pocas líneas, qué hace la API y quién la consume.
3. Módulos y entidades principales: para cada entidad, sus campos importantes, cuáles son obligatorios o únicos, y sus relaciones.
4. Origen del frontend para CORS (por ejemplo `https://localhost:5173`).
5. Solo modo 2: ¿las operaciones de escritura (crear, editar, eliminar) serán públicas, protegidas con API key o no existirán?

### Entrevista completa (modo 3), además del mínimo
- **Ronda A — Negocio:** problema y objetivos; actores; módulos; reglas de negocio importantes; estados y flujos; qué queda fuera de alcance.
- **Ronda B — Seguridad:** ¿hay usuarios? (si no, `Security=disabled`); ¿registro público o solo los crea un admin?; ¿2FA?; roles además de Admin y User, y qué puede hacer cada uno; ¿hay datos que solo puede ver su dueño?; ¿API key para sistemas externos?
- **Ronda C — Datos:** ¿int o Guid como PK?; ¿soft delete?; ¿historial de cambios (auditoría detallada)?; volumen esperado; datos sensibles y retención.
- **Ronda D — Capacidades e integraciones:** emails reales; archivos; tareas programadas; integraciones con terceros; tiempo real; caché; versionado; multi-empresa; idiomas. Usar los disparadores de `ai/suggestions-catalog.md` para guiar las preguntas.
- **Ronda E — Infraestructura:** dónde se despliega (Docker, Azure App Service, Container Apps, IIS); entornos; destino de logs; objetivos de rendimiento y disponibilidad (si no los sabe, proponer los valores por defecto de `docs/04`).

## Paso 4 — Sugerencias proactivas (todos los modos)
Con todo lo conversado, actuar como **solution-architect**:
1. Recorrer los disparadores de `ai/suggestions-catalog.md` y elegir las capacidades que encajan **con este proyecto**. Pensar también en lo que no está en el catálogo: riesgos, normativa del sector, casos borde.
2. **Si hay acceso web**, buscar referencias actuales: documentación oficial de Microsoft Learn, versión vigente en nuget.org y buenas prácticas recientes para el tipo de sistema. Dar el enlace de cada referencia.
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

## Paso 6 — Crear la solución (punto de control 2)
Delegar en **backend-developer**, aplicando solo lo que corresponde al perfil (las marcas `[SEC]` solo con `Security=enabled`):
1. Estructura y archivos raíz: `standards/14` (`.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `global.json`, `.gitignore`).
2. Proyecto y paquetes: `standards/01`.
3. Infraestructura común: `standards/02`, `02a`, `03`, `04` (base, persistencia, `Result`, specs, API) y los middlewares de `09` y `10`.
4. Con seguridad: `standards/05` a `05f` y `06` completos. Sin seguridad: `SystemCurrentUserService`, `AppDbContext : DbContext`, sin Auth. Con API key: la sección API key de `standards/09`.
5. Arranque: `standards/01a` (DI, `Program.cs`, `appsettings.json`) y user-secrets.
6. Capacidades aceptadas: implementar cada una según su referencia, o dejarla en `docs/10-roadmap.md` si se pospuso.
7. `dotnet build` **sin errores**. Si falla, corregir antes de seguir.
8. Migración `InitialCreate` (Identity y tablas base).

## Paso 7 — Entidades de negocio
Para cada entidad de `docs/05-domain-model.md`, en orden de dependencia (primero las entidades padre), ejecutar el flujo `ai/workflows/new-entity.md`. Compilar después de cada una. Al final, crear una migración por módulo (o por entidad, según convenga).

## Paso 8 — Tests y verificación
1. Crear los proyectos de test (`standards/12`) y el fixture `ApiFactory`.
2. `ai/workflows/test-generation.md` para los casos mínimos de cada entidad y, con seguridad, los de Auth.
3. `dotnet test` en verde (requiere Docker para Testcontainers; si no está disponible, avisar al usuario y dejar los tests listos).

## Paso 9 — Cierre
1. `docs/ai/PROJECT_STATUS.md`: qué quedó hecho, en curso y pendiente.
2. `docs/00-MASTER_CONTEXT.md`: estado actual.
3. Resumen al usuario:
   - Perfil y capacidades activas.
   - Módulos, entidades y endpoints creados.
   - Cómo correrlo: `dotnet run`, `/scalar` y, con seguridad, el admin del seed (contraseña en user-secrets).
   - Sugerencias pospuestas y preguntas abiertas.
   - Próximos pasos recomendados.

---

## Reglas
- **Nunca** escribir código antes del punto de control 1 aprobado.
- **Nunca** crear entidades que el usuario no definió, ni entidades de ejemplo.
- Ante una respuesta ambigua, preguntar. Si se supone algo para avanzar, marcarlo "(supuesto, confirmar)".
- Las decisiones las toma el usuario; la IA propone con opciones y una recomendación.
- Si el contexto es limitado (modelo local), trabajar un paso por sesión y guardar el avance en `PROJECT_STATUS.md` antes de continuar.

## Salida esperada
Documentación del proyecto llena, ADR-0001, solución compilando con el perfil elegido, entidades con sus endpoints, migraciones y tests.

## Definition of Done
- [ ] Sin marcadores `{{` en `docs/` (salvo plantillas `_TEMPLATE` y `ADR-0000`).
- [ ] Perfil completo y coherente con lo implementado.
- [ ] `dotnet build` y `dotnet test` en verde (o tests listos si no hay Docker, informado al usuario).
- [ ] Migraciones creadas.
- [ ] `PROJECT_STATUS.md` y `00-MASTER_CONTEXT.md` actualizados.
