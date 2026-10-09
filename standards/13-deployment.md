# Despliegue y producción

> **Aplica a:** Todos los perfiles (ítems `[SEC]` solo con seguridad)  
> **Propósito:** Entornos, configuración, Docker, CI con GitHub Actions, migraciones, Azure / IIS y checklist de producción.  
> Índice general: `standards/00-INDEX.md`

## Entornos y configuración
- `ASPNETCORE_ENVIRONMENT`: `Development`, `Testing` (tests de integración), `Staging`, `Production`.
- Orden de configuración de ASP.NET Core (el último gana): `appsettings.json` → `appsettings.{Env}.json` → user-secrets (solo Development) → variables de entorno → argumentos.
- `appsettings.{Env}.json` **sin secretos**. Los secretos llegan por variables de entorno o Key Vault.
- Variables de entorno con `__` para niveles: `ConnectionStrings__Default`, `Jwt__SigningKey`, `Cors__AllowedOrigins__0`.

## Docker
`Dockerfile` en la raíz del repositorio:
→ Código: [`templates/api/Dockerfile`](../templates/api/Dockerfile)
- Las imágenes oficiales de .NET escuchan en el puerto **8080** y traen un usuario sin privilegios (`$APP_UID`).
- Alternativa sin Dockerfile: `dotnet publish -c Release /t:PublishContainer` (soporte de contenedores del SDK).
- `.dockerignore` con `bin/`, `obj/`, `.git/`, `.vs/`, `**/appsettings.*.local.json`.

`docker-compose.yml` para desarrollo local (API + base de datos del perfil). Variante SQL Server:
→ Código: [`templates/api/docker-compose.yml`](../templates/api/docker-compose.yml)
Variante PostgreSQL (reemplaza el servicio `db` y la cadena de conexión):
```yaml
  db:
    image: postgres:17
    environment:
      POSTGRES_DB: "{project}_db"
      POSTGRES_USER: "{project}_app"
      POSTGRES_PASSWORD: "${PG_PASSWORD}"
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]
  # api → ConnectionStrings__Default: "Host=db;Port=5432;Database={project}_db;Username={project}_app;Password=${PG_PASSWORD}"
```
Usar la versión mayor de PostgreSQL acordada en el proyecto (la imagen oficial publica una etiqueta por versión).

Las variables (`SA_PASSWORD`, `JWT_SIGNING_KEY`) van en un archivo `.env` **que no se sube al repositorio** (agregarlo a `.gitignore`).

## Migraciones en producción
- **Nunca** `Database:ApplyMigrationsOnStartup = true` en producción: con varias instancias compiten, y la app necesitaría permisos de DDL.
- Opción recomendada, **migrations bundle** (ejecutable autocontenido):
```bash
dotnet ef migrations bundle -p src/{Project}.Api -o efbundle --self-contained -r linux-x64
./efbundle --connection "$ConnectionStrings__Default"
```
- Alternativa, script idempotente revisado por un DBA:
```bash
dotnet ef migrations script --idempotent -p src/{Project}.Api -o artifacts/migrate.sql
```
- La migración corre **antes** de desplegar la nueva versión. Cambios destructivos (borrar o renombrar columnas) en dos despliegues: primero compatible hacia atrás, después la limpieza.

## CI — `.github/workflows/ci.yml`
```yaml
name: ci
on:
  push: { branches: [main] }
  pull_request:

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - run: dotnet restore
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --coverage --coverage-output-format cobertura   # Testcontainers usa el Docker del runner
      - name: Paquetes vulnerables
        run: |
          dotnet list package --vulnerable --include-transitive | tee vuln.txt
          ! grep -qiE "critical|high" vuln.txt
```
El despliegue (CD) va en otro workflow o job, que solo corre con la CI en verde: publicar → migrations bundle → desplegar.

## Destinos de despliegue

### Azure App Service (Linux)
- Runtime .NET 10 o contenedor. Configuración en **App Settings** (equivalen a variables de entorno) y secretos con referencias a Key Vault.
- Health check del App Service apuntando a `/health/ready`.
- Despliegue desde GitHub Actions con `azure/webapps-deploy`, autenticando con OIDC (`azure/login`), sin secretos de publicación.

### Base de datos gestionada
- SQL Server → **Azure SQL Database** (o SQL Managed Instance). PostgreSQL → **Azure Database for PostgreSQL – Flexible Server**.
- Conexión con identidad administrada (Microsoft Entra ID) cuando sea posible, en lugar de usuario y contraseña.
- Backups automáticos del servicio + prueba de restauración periódica. On-premise: `BACKUP DATABASE` (SQL Server) o `pg_dump`/`pg_basebackup` (PostgreSQL).

### Azure Container Apps
- Imagen del Dockerfile en Azure Container Registry. Probes: liveness `/health/live`, readiness `/health/ready`.
- Secretos de Container Apps o Key Vault; escalado por peticiones HTTP.

### IIS (Windows Server, on-premise)
- Instalar el **ASP.NET Core Hosting Bundle** de .NET 10.
- `dotnet publish -c Release -o <carpeta>`; el `web.config` lo genera el publish (modelo in-process, `AspNetCoreModuleV2`).
- Application Pool con **"No Managed Code"**.
- Variables de entorno y secretos en la configuración del sitio o del sistema, nunca en archivos del repositorio.

## Checklist de producción
- [ ] `ASPNETCORE_ENVIRONMENT=Production`; OpenAPI y Scalar no expuestos públicamente (solo en Development, o protegidos).
- [ ] Cadena de conexión con un usuario de BD de **mínimos privilegios** (sin DDL; las migraciones usan otro usuario).
- [ ] HTTPS y HSTS activos; CORS solo con los orígenes reales.
- [ ] Rate limiting ajustado a `docs/04-non-functional-requirements.md`; `ReverseProxy:Enabled` + `KnownProxies`/`KnownNetworks` si hay proxy o balanceador (`standards/13a`).
- [ ] Health checks conectados al orquestador o balanceador.
- [ ] Observabilidad: OpenTelemetry o Application Insights con alertas (`standards/10`).
- [ ] Backups de la base de datos y prueba de restauración documentada en `docs/runbooks/`.
- [ ] `[SEC]` **Llaves de Data Protection persistentes**: firman los tokens de email y reset; si se pierden al reiniciar o hay varias instancias, los enlaces dejan de servir. Por ejemplo `services.AddDataProtection().PersistKeysToDbContext<AppDbContext>()` (paquete `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) o Azure Blob / Key Vault.
- [ ] `[SEC]` `Jwt:SigningKey` en Key Vault o variables de entorno, con rotación planificada.
- [ ] `[SEC]` `IEmailSender` real (SMTP con MailKit, SendGrid, etc.) y plantillas HTML.
- [ ] `[SEC]` Job programado que borre `RefreshTokens` expirados o revocados con más de N días (`ExecuteDeleteAsync`).
- [ ] `[SEC]` Límite de peticiones en Auth ajustado.
- [ ] Logs sin datos sensibles (nunca contraseñas, tokens ni códigos 2FA).
