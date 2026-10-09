# Referencias oficiales para la IA

Fuentes que la IA debe consultar para trabajar con información **actual** y no de memoria: versiones vigentes, APIs nuevas, cambios incompatibles y buenas prácticas de rendimiento y seguridad.

## Cómo buscar
1. **Primero el MCP de Microsoft Learn**, si la herramienta lo tiene conectado (`.mcp.json`, `.cursor/mcp.json`, `.vscode/mcp.json`). Busca y lee la documentación oficial de .NET, ASP.NET Core, EF Core, Azure y SQL Server directamente.
2. Si no hay MCP pero sí búsqueda web: restringir a fuentes oficiales, por ejemplo `site:learn.microsoft.com ef core execute update`.
3. **Versión correcta**: las páginas de Microsoft Learn tienen selector de versión (`?view=aspnetcore-10.0`). Leer la que coincide con `TargetFramework` del perfil.
4. **Versiones de paquetes**: confirmar siempre en nuget.org (o con `dotnet package search <paquete> --exact-match`), nunca de memoria.
5. **Orden de confianza**:
   1. Documentación oficial (Microsoft Learn, npgsql.org, postgresql.org).
   2. Repositorio oficial en GitHub (release notes, issues).
   3. Blog oficial (devblogs.microsoft.com).
   4. Comunidad (Stack Overflow, blogs), solo como pista y verificada contra lo oficial.
6. **Citar** el enlace consultado cuando una decisión o sugerencia dependa de él (ADR, sugerencias de `project-init`).
7. **Sin acceso web** (modelo local): usar este archivo y `ai/suggestions-catalog.md` como guía y marcar versiones y APIs como "verificar".

## .NET (plataforma y versiones)
| Tema | Enlace |
|---|---|
| Novedades de cada versión | https://learn.microsoft.com/dotnet/core/whats-new/ |
| Cambios incompatibles (breaking changes) | https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes |
| Política de soporte (LTS/STS y fechas) | https://dotnet.microsoft.com/platform/support/policy/dotnet-core |
| Notas de versión y descargas | https://github.com/dotnet/core/tree/main/release-notes |
| Índice de versiones (JSON) | https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json |
| Blog oficial de .NET | https://devblogs.microsoft.com/dotnet/ |
| Novedades de C# | https://learn.microsoft.com/dotnet/csharp/whats-new/ |
| Convenciones de código C# | https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions |
| Reglas de análisis de código (CAxxxx, IDExxxx) | https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview |
| Guías de arquitectura .NET | https://learn.microsoft.com/dotnet/architecture/ |

## ASP.NET Core (Web API)
| Tema | Enlace |
|---|---|
| Documentación principal | https://learn.microsoft.com/aspnet/core/ |
| Novedades por versión (cambiar el número para otras versiones) | https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0 |
| Web API con controllers | https://learn.microsoft.com/aspnet/core/web-api/ |
| Manejo de errores y ProblemDetails | https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api |
| OpenAPI integrado | https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview |
| Inyección de dependencias | https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection |
| Configuración y Options | https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options |
| Logging | https://learn.microsoft.com/aspnet/core/fundamentals/logging/ |
| Rate limiting | https://learn.microsoft.com/aspnet/core/performance/rate-limit |
| CORS | https://learn.microsoft.com/aspnet/core/security/cors |
| Health checks | https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks |
| Buenas prácticas de rendimiento | https://learn.microsoft.com/aspnet/core/fundamentals/best-practices |
| HybridCache | https://learn.microsoft.com/aspnet/core/performance/caching/hybrid |
| Output caching | https://learn.microsoft.com/aspnet/core/performance/caching/output |

## Seguridad
| Tema | Enlace |
|---|---|
| ASP.NET Core Identity | https://learn.microsoft.com/aspnet/core/security/authentication/identity |
| Autenticación JWT Bearer | https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication |
| Autorización por políticas | https://learn.microsoft.com/aspnet/core/security/authorization/policies |
| 2FA con apps autenticadoras (TOTP, QR) | https://learn.microsoft.com/aspnet/core/security/authentication/identity-enable-qrcodes |
| Data Protection | https://learn.microsoft.com/aspnet/core/security/data-protection/introduction |
| Secretos en desarrollo (user-secrets) | https://learn.microsoft.com/aspnet/core/security/app-secrets |
| Azure Key Vault como configuración | https://learn.microsoft.com/aspnet/core/security/key-vault-configuration |
| OWASP API Security Top 10 | https://owasp.org/API-Security/editions/2023/en/0x11-t10/ |
| OWASP REST Security Cheat Sheet | https://cheatsheetseries.owasp.org/cheatsheets/REST_Security_Cheat_Sheet.html |
| ProblemDetails (RFC 9457) | https://www.rfc-editor.org/rfc/rfc9457 |

## EF Core
| Tema | Enlace |
|---|---|
| Documentación principal | https://learn.microsoft.com/ef/core/ |
| Novedades y breaking changes por versión | https://learn.microsoft.com/ef/core/what-is-new/ |
| Rendimiento | https://learn.microsoft.com/ef/core/performance/ |
| Migraciones | https://learn.microsoft.com/ef/core/managing-schemas/migrations/ |
| Aplicar migraciones (bundles, scripts) | https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying |
| Concurrencia optimista | https://learn.microsoft.com/ef/core/saving/concurrency |
| ExecuteUpdate / ExecuteDelete | https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete |
| Filtros globales (soft delete, multi-tenant) | https://learn.microsoft.com/ef/core/querying/filters |
| Interceptores | https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors |
| SQL crudo parametrizado | https://learn.microsoft.com/ef/core/querying/sql-queries |
| Proveedor SQL Server | https://learn.microsoft.com/ef/core/providers/sql-server/ |

## PostgreSQL
| Tema | Enlace |
|---|---|
| Proveedor EF Core de Npgsql | https://www.npgsql.org/efcore/ |
| Documentación de Npgsql | https://www.npgsql.org/doc/ |
| Fechas y zonas horarias en Npgsql | https://www.npgsql.org/doc/types/datetime.html |
| EFCore.NamingConventions (snake_case) | https://github.com/efcore/EFCore.NamingConventions |
| Documentación de PostgreSQL | https://www.postgresql.org/docs/current/ |
| `pg_trgm` (búsqueda `ILIKE` rápida) | https://www.postgresql.org/docs/current/pgtrgm.html |
| `EXPLAIN` (planes de ejecución) | https://www.postgresql.org/docs/current/using-explain.html |
| Azure Database for PostgreSQL (Flexible Server) | https://learn.microsoft.com/azure/postgresql/flexible-server/overview |

## SQL Server
| Tema | Enlace |
|---|---|
| Documentación de SQL Server | https://learn.microsoft.com/sql/sql-server/ |
| Query Store | https://learn.microsoft.com/sql/relational-databases/performance/monitoring-performance-by-using-the-query-store |
| Tablas temporales con EF Core | https://learn.microsoft.com/ef/core/providers/sql-server/temporal-tables |
| Azure SQL | https://learn.microsoft.com/azure/azure-sql/ |

## Analítica de producto
| Tema | Enlace |
|---|---|
| Channels (colas en memoria) | https://learn.microsoft.com/dotnet/core/extensions/channels |
| Servicios en segundo plano (BackgroundService) | https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services |
| HMACSHA256 (seudonimización) | https://learn.microsoft.com/dotnet/api/system.security.cryptography.hmacsha256 |
| JSON en SQL Server | https://learn.microsoft.com/sql/relational-databases/json/json-data-sql-server |
| Tipos JSON en PostgreSQL (`jsonb`) | https://www.postgresql.org/docs/current/datatype-json.html |
| GA4 Measurement Protocol (si un proyecto agrega ese destino) | https://developers.google.com/analytics/devguides/collection/protocol/ga4 |

## Testing
| Tema | Enlace |
|---|---|
| Buenas prácticas de pruebas unitarias | https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices |
| Pruebas de integración en ASP.NET Core | https://learn.microsoft.com/aspnet/core/test/integration-tests |
| xUnit | https://xunit.net/ |
| Testcontainers para .NET | https://dotnet.testcontainers.org/ |
| Respawn | https://github.com/jbogard/Respawn |

## Observabilidad y diagnóstico
| Tema | Enlace |
|---|---|
| OpenTelemetry en .NET (Microsoft) | https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel |
| OpenTelemetry .NET (proyecto) | https://opentelemetry.io/docs/languages/dotnet/ |
| dotnet-counters | https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-counters |
| dotnet-trace | https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace |

## Despliegue
| Tema | Enlace |
|---|---|
| .NET en contenedores | https://learn.microsoft.com/dotnet/core/docker/introduction |
| Hospedar en IIS | https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/ |
| Linux con Nginx | https://learn.microsoft.com/aspnet/core/host-and-deploy/linux-nginx |
| Detrás de proxies y balanceadores (Forwarded Headers) | https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer |
| Documentación de Nginx | https://nginx.org/en/docs/ |
| YARP (reverse proxy en .NET) | https://learn.microsoft.com/aspnet/core/fundamentals/servers/yarp/yarp-overview |
| Azure App Service | https://learn.microsoft.com/azure/app-service/ |
| Azure Container Apps | https://learn.microsoft.com/azure/container-apps/overview |
| GitHub Actions para .NET | https://docs.github.com/actions/use-cases-and-examples/building-and-testing/building-and-testing-net |

## Librerías del stack
| Librería | Enlace |
|---|---|
| NuGet (versiones de paquetes) | https://www.nuget.org/ |
| FluentValidation | https://docs.fluentvalidation.net/ |
| Scalar (UI de OpenAPI) | https://github.com/scalar/scalar |
| Guía de diseño de APIs (Azure Architecture Center) | https://learn.microsoft.com/azure/architecture/best-practices/api-design |
