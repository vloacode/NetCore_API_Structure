# NetCoreApiKit

**Asistente paso a paso para crear APIs ASP.NET Core (.NET 10) listas para producción**, con una arquitectura fija y una guía para que cualquier IA (Claude Code, opencode, Hermes Agent, Cursor, Copilot, Codex — en la nube o con modelos locales) siga construyendo el proyecto de la misma forma.

*Step-by-step wizard (like `npm create vue@latest`) that scaffolds a production-ready ASP.NET Core Web API: Controllers, EF Core with Repository + Unit of Work, optional local Identity + JWT, SQL Server or PostgreSQL, tests with Testcontainers, CI, Docker, and an AI guide for coding agents. Docs in Spanish.*

```bash
dnx NetCoreApiKit
```

Requisitos: **.NET 10 SDK**. Docker solo si la base de datos va en un contenedor.

---

## Qué pregunta

1. **Nombre** del proyecto (PascalCase, ej. `MiApi`).
2. **Tipo de API**: con usuarios (login JWT, refresh tokens, 2FA, roles y permisos) o pública. Si es pública, si las escrituras se protegen con **API key**.
3. **Base de datos**: PostgreSQL o SQL Server.
4. **Emails** (con usuarios): SMTP real con MailKit (más Mailpit para probar en local) o solo al log.
5. **Extras**: analítica de uso del producto.
6. **Dónde está la base**:
   - **Docker**: crea el contenedor, con clave aleatoria y un puerto libre;
   - **instalada en tu máquina** (PostgreSQL, SQL Server, LocalDB o Express, con autenticación de Windows si quieres);
   - **un servidor** (host, usuario, contraseña oculta, SSL / TrustServerCertificate);
   - **después**.

   Antes de guardar nada, **prueba la conexión** y dice qué falló si no conecta.

   Si la base **ya tiene tablas** (por ejemplo, Identity creado por otra app), te muestra cuáles hay y cuántas filas tienen, y pregunta qué hacer. **Nada se borra**:
   - **Respaldar y crear nuevas** (recomendado si hay datos): las tablas que chocan se mueven intactas, con datos, índices y relaciones, a un esquema `kit_backup_<fecha>` de la misma base, y se crean las nuevas.
   - **Usar las existentes y crear lo que falte**: crea solo las tablas y columnas que faltan, copia antes en `kit_backup_<fecha>` las tablas que modifica y registra la migración como aplicada.
   - **Usar otra base** (otro nombre) o **no tocarla**.

   Antes de aplicar te muestra el plan, con la opción de ver el SQL exacto, y todo va en una transacción.
7. **Dónde guardar los secretos**: user-secrets, variables de entorno y/o `appsettings.Development.local.json` (ignorado por git). Puedes marcar uno, varios o todos.
8. **Git**: inicializar el repositorio o no.

Luego genera la solución, copia la guía para IA, levanta la base, guarda los secretos, compila, crea la migración inicial y te muestra cómo correrla, incluida la contraseña del admin, que se ve **una sola vez**.

## Sin preguntas (IA, CI, scripts)

Cada pregunta tiene su opción:

```bash
# API con usuarios + PostgreSQL en Docker + emails SMTP
dnx NetCoreApiKit -- --name MiApi --security --database postgresql --email smtp --db-location docker --yes

# API pública + SQL Server en un servidor existente; la conexión viaja por variable de entorno
KIT_DB_CONNECTION="Server=sql01;Database=MiApiDb;User Id=app;Password=...;TrustServerCertificate=True" \
  dnx NetCoreApiKit -- --name MiApi --public --apikey --database sqlserver --db-location server --yes

# Secretos en todos los destinos
dnx NetCoreApiKit -- --name MiApi --security --database postgresql --db-location docker --secrets user-secrets,env,appsettings --yes
```

| Opción | Valores | Por defecto |
|---|---|---|
| `-n`, `--name` | Nombre en PascalCase | (se pregunta) |
| `--security` / `--public` | API con usuarios o pública | con usuarios |
| `--apikey` | Escrituras con `X-Api-Key` (solo pública) | no |
| `--database` | `postgresql` · `sqlserver` | `postgresql` |
| `--email` | `smtp` · `log` (solo con usuarios) | `smtp` |
| `--analytics` | Analítica de uso | no |
| `--db-location` | `docker` · `local` · `server` · `later` | `docker` |
| `--connection` | Cadena de conexión (mejor: variable `KIT_DB_CONNECTION`) | — |
| `--apply-migrations` / `--no-apply-migrations` | Crear las tablas | sí en docker/local, **no** en server |
| `--existing-db` | Si la base ya tiene tablas: `backup` · `reuse` · `skip` | se pregunta (sin preguntas: obligatorio) |
| `--secrets` | `user-secrets`, `env`, `appsettings` (separados por coma) | `user-secrets` |
| `--persist-env` | Con `env` en Windows: guardar también como variables de usuario | no |
| `--git` / `--no-git` | Inicializar git | sí |
| `-o`, `--output` | Carpeta destino | `./<nombre>` |
| `-y`, `--yes` | No preguntar nada | — |

`dnx NetCoreApiKit -- --help` muestra la ayuda completa.

## Qué genera

```
MiApi/
├── MiApi.slnx · Directory.Build.props · Directory.Packages.props · Dockerfile · docker-compose.yml
├── src/MiApi.Api/
│   ├── Program.cs              ← índice del arranque: servicios por área + pipeline HTTP
│   ├── Extensions/             ← Persistence, Security, Application, Api, Observability
│   ├── Features/{Entidades}/   ← una carpeta por entidad, 4 archivos (dotnet new kit-entity)
│   ├── Domain/ · Application/ · Infrastructure/ · Api/
├── tests/MiApi.IntegrationTests/   ← xUnit v3 + Testcontainers (base real) + tests de arquitectura
├── .github/workflows/ci.yml + dependabot.yml
└── AGENTS.md · ai/ · standards/ · docs/ · adaptadores para cada IA
```

- **Arquitectura**: Controllers → services con `Result<T>` → Repository + Unit of Work + Specification sobre EF Core. FluentValidation, ProblemDetails (RFC 9457), OpenAPI + Scalar. Sin MediatR ni AutoMapper.
- **Seguridad** (opcional): ASP.NET Core Identity local + JWT, refresh tokens con rotación y detección de reuso, 2FA TOTP, permisos por rol, bitácora de seguridad (`/api/security-events`), llaves de Data Protection persistentes.
- **Robustez**: auditoría y soft delete automáticos, concurrencia optimista, rate limiting por usuario o IP, timeouts por request, health checks, OpenTelemetry (trazas, métricas y logs), headers de seguridad, Forwarded Headers para proxies.
- **Calidad**: analizadores con warnings como errores en Release, `dotnet format`, tests con la base real, CI con chequeo de paquetes vulnerables.

## Después de crear el proyecto

```bash
cd MiApi
dotnet run --project src/MiApi.Api                     # https://localhost:7100/scalar
dotnet new kit-entity -n Customer --app MiApi --security true --database postgresql
dotnet test                                            # requiere Docker (Testcontainers)
```

**Con IA**: abre la carpeta y escribe `/project-init` (Claude Code u opencode) o pide "lee AGENTS.md y ejecuta ai/workflows/project-init.md". La IA sigue con la entrevista del negocio, la documentación y las entidades, **fase por fase**.

| Herramienta | Qué usa del proyecto |
|---|---|
| Claude Code | `CLAUDE.md`, `.claude/skills/`, `.claude/agents/`, `.mcp.json` |
| opencode | `AGENTS.md`, `.opencode/commands/`, `.opencode/agents/`, `opencode.json` |
| Hermes Agent | `AGENTS.md`, `.agents/skills/` (correr `hermes skills trust` una vez) |
| Cursor · Copilot · Codex | `.cursor/rules/`, `.github/copilot-instructions.md`, `AGENTS.md`, `.agents/skills/` |

**Modelos locales** (Ollama, llama.cpp, LM Studio, vLLM): sírvelos con **32K de contexto o más** y **tool calling** activo. Si no, la IA no ve las instrucciones ni las skills. Detalle en `ai/tools-setup.md` dentro del proyecto.

## Más información

- Repositorio, plantillas y documentación: https://github.com/vloacode/NetCore_API_Structure
- Novedades de cada versión: `CHANGELOG.md` del repositorio
