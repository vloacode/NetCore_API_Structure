# Verificación de los standards

Estas herramientas comprueban que **el código C# escrito en `standards/` compila y funciona**, en los dos perfiles (con seguridad y API pública). Se usan al modificar el kit, no en los proyectos.

## Qué hace `verify-standards.py`
Por cada perfil, en una carpeta temporal `.verify/` (ignorada por git):
1. Crea un proyecto `dotnet new webapi` con los paquetes del perfil y los `Directory.Build.props` / `.editorconfig` de `standards/14`.
2. `materialize.py` extrae cada bloque de código de `standards/` que tenga ruta de archivo y lo escribe en el proyecto, aplicando las marcas `[SEC]` / `[PUB]`. Las plantillas de `standards/07` se compilan con nombres de prueba (`VerifyItem`, `VerifyParent`) que solo existen en esa carpeta temporal.
3. `dotnet format` y `dotnet format --verify-no-changes`.
4. `dotnet build -c Release`, con analizadores y warnings como errores.
5. Con `--smoke`, además:
   - Crea la migración y arranca la API contra SQL Server (aplica la migración al iniciar).
   - Corre `smoke_secure.py` (32 pruebas: auth, 2FA, refresh con rotación y reuso, permisos, CRUD, concurrencia, soft delete, errores, rate limit) o `smoke_public.py` (8 pruebas).
   - Al final borra la base de datos temporal.

## Uso
```bash
python scripts/verify/verify-standards.py
python scripts/verify/verify-standards.py --profiles public
python scripts/verify/verify-standards.py --smoke "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
```
Requisitos: .NET 10 SDK y Python 3. Para `--smoke`: `dotnet-ef`, `sqlcmd` y un SQL Server accesible (LocalDB en Windows sirve).

La CI (`.github/workflows/kit-checks.yml`) corre la verificación sin `--smoke` en cada push.

## Al cambiar un standard
- Todo bloque C# que represente un archivo debe llevar su ruta entre comillas invertidas en las 3 líneas anteriores (por ejemplo, `` `Infrastructure/Persistence/UnitOfWork.cs` ``).
- Lo que solo aplica con seguridad lleva `// [SEC]`; lo que solo aplica sin seguridad, `// [PUB]`.
- Correr `verify-standards.py`, y `--smoke` si el cambio afecta comportamiento.
