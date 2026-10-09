# Flujo: upgrade-dotnet (migrar un proyecto a otra versión de .NET)

## Objetivo
Llevar un proyecto existente a una versión más nueva de .NET (por ejemplo, de .NET 10 a .NET 12) **de forma controlada y verificable**, sin romper la API ni a sus consumidores.

## Cuándo usarlo
- Se acerca el fin de soporte de la versión actual del proyecto.
- El usuario quiere una funcionalidad que solo existe en una versión nueva.
- El kit adoptó una LTS nueva (`upgrade-kit` lo recomendará).

## Roles
**devops-engineer** (SDK, CI, Docker) · **backend-developer** (cambios de código) · **qa-engineer** (tests) · **solution-architect** (ADR).

## Archivos a leer
`standards/16-framework-versions.md`, `ai/references.md` (What's new y Breaking changes), `docs/00-MASTER_CONTEXT.md` (`TargetFramework`) y `KIT_VERSION`.

## Pasos
1. **Situación actual**: versión del proyecto (perfil y `Directory.Build.props`), SDKs instalados (`dotnet --list-sdks`) y fechas de soporte de la versión actual y de la destino (política oficial).
2. **Elegir el destino**: por defecto, la LTS vigente. Una STS requiere ADR. Confirmar con el usuario.
3. **Investigar los cambios**: leer *Breaking changes* de .NET, ASP.NET Core y EF Core de **cada versión intermedia** (si se salta de 10 a 12, también la 11). Con acceso web, usar las fuentes de `ai/references.md` (o el MCP de Microsoft Learn). Listar los que afectan al proyecto: APIs usadas, paquetes y comportamiento.
4. **Plan**: lista de cambios por archivo y riesgos. **Mostrarlo al usuario y esperar su aprobación.**
5. **Rama** `chore/upgrade-dotnet-<version>`.
6. **Actualizar versiones** (tabla "Dónde aparece la versión" de `standards/16`):
   - `Directory.Build.props` (`TargetFramework`) y `global.json`.
   - `Directory.Packages.props`: `MicrosoftVersion`, las versiones de Npgsql/EFCore.NamingConventions y librerías de terceros a versiones compatibles.
   - `Dockerfile` (`ARG DOTNET_VERSION`), CI (`dotnet-version`) y la herramienta `dotnet-ef` (`dotnet tool update --global dotnet-ef`).
7. **Compilar** (`dotnet build -c Release`) y corregir errores y warnings nuevos (analizadores nuevos incluidos), apoyándose en la lista de breaking changes.
8. **Migraciones**: `dotnet ef migrations add UpgradeDotnet<version>`. Si el modelo no cambió, la migración debe quedar vacía; si genera cambios, revisarlos (algunas versiones cambian convenciones) con `database-change`.
9. **Pruebas**: `dotnet test` completo y arrancar la API para una prueba manual de los endpoints clave en `/scalar`.
10. **Rendimiento** (opcional pero recomendado): comparar los endpoints críticos antes y después (`performance-review`).
11. **Documentar**:
    - `TargetFramework` en el perfil.
    - ADR con la decisión y los breaking changes atendidos.
    - `PROJECT_STATUS`.
    - Lecciones en `AI_MEMORY`.
12. **PR** con el resumen de cambios y el resultado de las pruebas.

## Reglas
- Una versión mayor por vez si el salto es grande y hay muchos breaking changes; si son pocos, se puede saltar directo a la LTS, pero leyendo los cambios de las versiones intermedias.
- No mezclar la migración de versión con funcionalidades nuevas en el mismo PR.
- Nunca subir a una versión preview en un proyecto productivo.

## Definition of Done
- [ ] Proyecto compilando en Release sin warnings nuevos, con la versión destino.
- [ ] Tests en verde y prueba manual hecha.
- [ ] Docker y CI actualizados.
- [ ] Perfil, ADR, `PROJECT_STATUS` y `AI_MEMORY` actualizados.
