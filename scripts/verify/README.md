# Verificación de los standards

Estas herramientas comprueban que **el código C# escrito en `standards/` compila y funciona** en la matriz **perfil** (con seguridad / API pública) × **motor** (SQL Server / PostgreSQL), y con la versión de .NET que se indique. Se usan al modificar el kit, no en los proyectos.

## Qué hace `verify-standards.py`
Por cada combinación perfil × motor, en una carpeta temporal `.verify/` (ignorada por git):
1. Crea un proyecto `dotnet new webapi` (con el `--framework` indicado, por defecto `net10.0`) con los paquetes del perfil y del motor y los `Directory.Build.props` / `.editorconfig` de `standards/14`.
2. `materialize.py` extrae cada bloque de código de `standards/` que tenga ruta de archivo y lo escribe en el proyecto, aplicando las marcas `[SEC]` / `[PUB]` / `[MSSQL]` / `[PGSQL]`. Las plantillas de `standards/07` se compilan con nombres de prueba (`VerifyItem`, `VerifyParent`) que solo existen en esa carpeta temporal.
3. Genera la migración `InitialCreate` (valida el modelo de EF Core con el proveedor real) y aplica `dotnet format` + `--verify-no-changes`.
4. `dotnet build -c Release`, con analizadores y warnings como errores.
5. Con `--smoke-sqlserver` / `--smoke-postgresql`, además:
   - Arranca la API contra la base real (aplica la migración y siembra un registro padre al iniciar).
   - Corre `smoke_secure.py` (32 pruebas: auth, 2FA, refresh con rotación y reuso, permisos, CRUD, concurrencia, soft delete, errores, rate limit) o `smoke_public.py` (8 pruebas).
   - Al final borra la base de datos temporal.

## Uso
```bash
python scripts/verify/verify-standards.py
python scripts/verify/verify-standards.py --profiles public --databases postgresql
python scripts/verify/verify-standards.py --framework net11.0
python scripts/verify/verify-standards.py --smoke-sqlserver "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
python scripts/verify/verify-standards.py --smoke-postgresql "Host=localhost;Port=5432;Username=postgres;Password=<tu-clave>"
```
Requisitos: SDK de .NET del framework elegido, Python 3 y `dotnet-ef`. Para las pruebas de humo, un servidor accesible del motor (LocalDB en Windows sirve para SQL Server). La base temporal se borra al final si están `sqlcmd` o `psql` en el PATH.

La CI (`.github/workflows/kit-checks.yml`) corre la matriz completa sin pruebas de humo en cada push.

## Al cambiar un standard
- Todo bloque C# que represente un archivo debe llevar su ruta entre comillas invertidas en las 3 líneas anteriores (por ejemplo, `` `Infrastructure/Persistence/UnitOfWork.cs` ``).
- Marcas: `// [SEC]` solo con seguridad, `// [PUB]` solo sin seguridad, `// [MSSQL]` solo SQL Server, `// [PGSQL]` solo PostgreSQL.
- Correr `verify-standards.py`, y las pruebas de humo si el cambio afecta comportamiento.
