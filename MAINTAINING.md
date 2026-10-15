# Mantener el kit

Guía para quien modifica y publica el kit (no para quien lo usa en un proyecto: eso está en `README.md`).
Este archivo no se copia a los proyectos generados.

## Ramas
Cada versión mayor de .NET que el kit soporta tiene su propia rama, con sus plantillas, standards y verificación.

| Rama | Contenido | Uso |
|---|---|---|
| `Net10` | Kit para .NET 10 (LTS) | **Aquí se trabaja** |
| `main` | Copia de la rama de la LTS más nueva (hoy `Net10`); la usa *Use this template* | Se adelanta (`git merge --ff-only`) al cerrar cada versión |
| `Net11`, `Net12`… | Se crean cuando salga esa versión | — |

Reglas:
1. Los cambios se hacen en la rama de la versión. Al cerrar una versión del kit (`KIT_VERSION` + entrada en `CHANGELOG.md`), `main` se adelanta a esa rama.
2. Una versión nueva de .NET **no** reemplaza la rama anterior: se crea `NetXX` desde la rama de la LTS vigente. La anterior recibe correcciones mientras su .NET tenga soporte.
3. Si la nueva es **LTS**, `main` pasa a seguirla. Si es **STS**, `main` se queda en la LTS.
4. Una corrección que aplica a varias versiones se hace en la rama más antigua con soporte y se lleva a las demás (`git cherry-pick`).
5. Cada rama tiene su propio `KIT_VERSION` y `CHANGELOG.md`; la rama nueva arranca con un MAJOR nuevo.

## Adaptadores de las herramientas de IA
Al agregar, renombrar o borrar un flujo (`ai/workflows/`) o un rol (`ai/roles/`):
1. Crear o actualizar su adaptador de Claude Code (`.claude/skills/<flujo>/SKILL.md` o `.claude/agents/<rol>.md`); su `description` es la que usan las demás herramientas.
2. Correr `python scripts/sync-adapters.py`: regenera `.agents/skills/` (Hermes, agentskills.io), `.opencode/commands/` y `.opencode/agents/`.
3. `python scripts/check-kit.py` falla si alguno quedó desactualizado.

Qué lee cada herramienta y cómo configurar modelos locales: `ai/tools-setup.md`.

## Versionado del kit
`KIT_VERSION` + `CHANGELOG.md` (formato Keep a Changelog):
- **MAJOR**: cambio incompatible para los proyectos (estructura, plantillas que obligan a migrar código).
- **MINOR**: capacidades, opciones o flujos nuevos compatibles.
- **PATCH**: correcciones.

## Verificar antes de publicar
| Herramienta | Qué comprueba | Cuándo |
|---|---|---|
| `python scripts/check-kit.py` | Enlaces, adaptadores de skills/subagentes, tamaño de archivos, nombres de ejemplo, código largo en los md | Siempre (CI) |
| `python scripts/verify/verify-template.py` | Plantillas `kitapi` + `kit-entity` en la matriz perfil × motor: migración, `format`, Release. Con `--run-tests`, tests con Testcontainers; con `--smoke-sqlserver` / `--smoke-postgresql`, pruebas de punta a punta | Al cambiar `templates/` (CI con `--run-tests`) |
| `python scripts/verify/verify-wizard.py` | El asistente empaquetado y ejecutado con `dnx`: Docker, servidor, secretos, contraseña incorrecta. `--quick` = solo el caso sin base | Al cambiar `tools/NetCoreApiKit/` (CI con `--quick`) |
| `python scripts/verify/verify-nginx.py` | La API detrás de un Nginx real | Al cambiar `standards/13a` o Forwarded Headers |

Detalle de cada script: `scripts/verify/README.md`.

Probar el asistente sin publicar:
```bash
dotnet run --project tools/NetCoreApiKit
```

## Publicar una versión del asistente (`dnx NetCoreApiKit`)
1. En la rama de la versión: subir `KIT_VERSION` y escribir la entrada del `CHANGELOG.md`.
2. Commit, push y adelantar `main` si es la rama de la LTS más nueva.
3. Crear y subir el tag igual a `KIT_VERSION`:
   ```bash
   git tag -a v3.4.1 -m "Kit 3.4.1"
   git push origin v3.4.1
   ```
4. `.github/workflows/publish-tool.yml` verifica (`verify-wizard.py --quick`), empaqueta y publica en nuget.org con **Trusted Publishing** (OIDC, sin API key guardada). Es automático, sin aprobación.
5. nuget.org tarda unos minutos en validar e indexar. Después funciona `dnx NetCoreApiKit`.
6. En paralelo, `.github/workflows/github-release.yml` crea el **GitHub Release** con las notas de esa versión del `CHANGELOG.md` (por eso la entrada del CHANGELOG debe estar antes del tag). Para un tag ya existente: *Actions → github-release → Run workflow* con el tag.

Configuración de Trusted Publishing (ya hecha): política en nuget.org con owner `vloacode`, repositorio `NetCore_API_Structure`, workflow `publish-tool.yml` y environment `nuget`; el usuario de nuget.org va en el workflow. Si el tag no coincide con `KIT_VERSION`, el workflow falla antes de publicar.

## Adoptar una versión nueva de .NET
1. Crear la rama desde la LTS vigente: `git switch -c Net11 Net10`.
2. Leer *What's new* y *Breaking changes* de .NET, ASP.NET Core, EF Core y C# (enlaces en `ai/references.md`).
3. Con el SDK nuevo instalado:
   ```bash
   python scripts/verify/verify-template.py --framework net11.0 --run-tests
   ```
4. Corregir las plantillas hasta que la matriz pase. Si una API cambió, actualizar el código **y** la tabla "Qué partes dependen de la versión" de `standards/16`.
5. Si pasa a ser la base: en `templates/`, el `framework` por defecto de `.template.config/template.json`, `Directory.Build.props`, `Dockerfile`, `global.json` y las versiones de `Directory.Packages.props`; en `tools/NetCoreApiKit`, el `TargetFramework`; el job de CI; el `CHANGELOG`.
6. Agregar la versión a la matriz de CI cuando su SDK esté en los runners de GitHub.
