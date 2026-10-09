# Verificación de las plantillas

Estas herramientas comprueban que **las plantillas `dotnet new` del kit** (`templates/api` → `kitapi`, `templates/entity` → `kit-entity`) generan proyectos que compilan y funcionan en la matriz **perfil** (con seguridad / API pública) × **motor** (SQL Server / PostgreSQL), con la versión de .NET que se indique. Se usan al modificar el kit, no en los proyectos.

## Qué hace `verify-template.py`
1. Instala las dos plantillas en un *hive* propio (`.verify/hive`): no toca las plantillas instaladas del usuario.
2. Por cada combinación, en `.verify/<combinación>/` (ignorada por git):
   - `dotnet new kitapi` con las opciones del perfil (con `--analytics true`) y, además, una combinación **mínima**: pública + PostgreSQL + `--apikey`, sin analytics.
   - `dotnet new kit-entity` dos veces: `VerifyParent` (sin padre) y `VerifyItem --parent VerifyParent`. Aplica las líneas del comentario REGISTRO como lo haría la IA (`DbSet` y permisos). Estos nombres de prueba solo existen en `.verify/`.
   - `dotnet ef migrations add InitialCreate` (valida el modelo con el proveedor real), `dotnet format --verify-no-changes` y `dotnet build -c Release` (analizadores, warnings como errores) de la API y los tests.
3. Con `--run-tests`, ejecuta los tests de integración generados (xUnit v3 + Testcontainers + Respawn; requiere Docker).
4. Con `--smoke-sqlserver` / `--smoke-postgresql`, arranca la API contra la base real (migración + un `VerifyParent` sembrado) y corre `smoke_secure.py` (35 pruebas: auth, 2FA, refresh con rotación y reuso, permisos, CRUD, concurrencia, soft delete, errores, analítica, rate limit) o `smoke_public.py` (11 pruebas). Al final borra la base temporal si están `sqlcmd` o `psql` en el PATH.

## Uso
```bash
python scripts/verify/verify-template.py
python scripts/verify/verify-template.py --profiles public --databases postgresql
python scripts/verify/verify-template.py --framework net11.0
python scripts/verify/verify-template.py --run-tests
python scripts/verify/verify-template.py --smoke-sqlserver "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
python scripts/verify/verify-template.py --smoke-postgresql "Host=localhost;Port=5432;Username=postgres;Password=<tu-clave>"
```
Requisitos: SDK de .NET del framework elegido, Python 3 y `dotnet-ef`. Para las pruebas de humo, un servidor del motor (LocalDB en Windows sirve para SQL Server; para PostgreSQL, un contenedor temporal con una clave generada).

La CI (`.github/workflows/kit-checks.yml`) corre la matriz completa con `--run-tests` (los runners de GitHub tienen Docker) en cada push.

## `verify-nginx.py` (API detrás de Nginx)
Levanta un contenedor `nginx:stable-alpine` con el `nginx.conf` de `standards/13a` (adaptado a HTTP local) delante de la API y comprueba:
- Que Nginx acepta la configuración.
- La IP que ve la API sin y con `ReverseProxy`.
- Que un `X-Forwarded-For` falsificado no cambia la IP ni evita el rate limiting.
- Que los headers de seguridad y gzip llegan bien.

Requiere Docker y un proyecto con seguridad ya generado por `verify-template.py`:
```bash
python scripts/verify/verify-nginx.py --project .verify/secure-sqlserver/KitVerify/src/KitVerify.Api --connection "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
```

## Al cambiar una plantilla
- Las variantes se escriben con condiciones del motor de plantillas: `#if (security)`, `#if (!security)`, `#if (sqlserver)`, `#if (postgresql)`, `#if (analytics)`, `#if (useApiKey)` en C#; `<!--#if (...) -->` en XML y `//#if (...)` en JSON. Archivos completos de un perfil: `sources.modifiers` en `.template.config/template.json`.
- Correr `verify-template.py` (y `--run-tests` y las pruebas de humo si el cambio afecta comportamiento) y `python scripts/check-kit.py`.
- Si el cambio altera una regla, actualizar el standard que enlaza ese archivo.
