# Arranque: DependencyInjection, Program.cs y configuración

> **Aplica a:** Todos los perfiles (líneas `[SEC]` solo con seguridad)  
> **Propósito:** Registro de servicios, pipeline HTTP, configuración y secretos.  
> Índice general: `standards/00-INDEX.md`

## `DependencyInjection.cs`
Cuatro métodos: `AddApplication`, `AddPersistence`, `AddSecurity` `[SEC]` y `AddApi`.

```csharp
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;        // [SEC]
using Microsoft.AspNetCore.Authorization;                   // [SEC]
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;                        // [SEC]
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;                       // [SEC]
using {Project}.Api.Authorization;                          // [SEC]
using {Project}.Api.Controllers;
using {Project}.Api.Errors;
using {Project}.Api.Filters;
using {Project}.Api.OpenApi;                                // [SEC]
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Features.Auth;                  // [SEC]
using {Project}.Infrastructure.Email;
using {Project}.Infrastructure.Identity;                    // [SEC]
using {Project}.Infrastructure.Persistence;
using {Project}.Infrastructure.Persistence.Interceptors;
using {Project}.Infrastructure.Services;                    // [PUB]

namespace {Project};

public static class DependencyInjection
{
    public const string CorsPolicy = "Default";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

        // Servicios de negocio: una línea por entidad (standards/07).
        // services.AddScoped<I{Entity}Service, {Entity}Service>();

        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure())          // [MSSQL]
            .UseNpgsql(configuration.GetConnectionString("Default"), npgsql => npgsql.EnableRetryOnFailure())       // [PGSQL]
            .UseSnakeCaseNamingConvention()                                                                         // [PGSQL]
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Health checks (standards/10): /health/ready verifica la base de datos.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

        // Email (reemplazar por SMTP/proveedor real en producción).
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        // Sin seguridad no hay usuario autenticado: la auditoría guarda solo fechas.
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();   // [PUB]

        return services;
    }

    // [SEC] — todo este método solo existe con seguridad.
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppUrlOptions>().BindConfiguration(AppUrlOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        // Identity local: sin cookies, sin proveedores externos.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = true;

                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredUniqueChars = 4;

                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();   // email confirm, reset password, change email, authenticator (TOTP)

        // Vigencia de los tokens de email (confirmación, reset, cambio de email).
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(3));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;   // claims con su nombre JWT ("sub", "role", "permission")
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,   // el token de 2FA usa otra audiencia y es rechazado
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Subject,
                    RoleClaimType = ClaimNames.Role
                };
            });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<SessionManager>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleService, RoleService>();

        return services;
    }

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(o => o.Filters.Add<ValidationFilter>())
            // JSON amigable para el frontend (standards/15): camelCase (default) + enums como texto.
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Todo ProblemDetails (excepciones, 404/405 vacíos, etc.) lleva el mismo traceId que el header X-Trace-Id (standards/08).
        // Se sobrescribe a propósito: el framework pone por defecto el traceparent completo ("00-…-00").
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());   // [SEC]
        services.AddOpenApi();                                                                     // [PUB]

        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Trace-Id", "Retry-After", "Content-Disposition")));   // legibles desde el navegador (standards/15)

        // Detrás de un reverse proxy o balanceador (Nginx, YARP, Azure Front Door…): IP real del cliente y esquema HTTPS.
        // Solo se confía en los proxies declarados; se activa con ReverseProxy:Enabled (standards/13a).
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = 1;   // un solo salto de proxy: el cliente no puede falsificar su IP con X-Forwarded-For
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(proxy));
            foreach (var network in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
                o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            // Límite global por IP para todos los endpoints (imprescindible en una API pública).
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            // [SEC] Límite estricto para los endpoints de Auth.
            o.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }
}
```

`Infrastructure/Services/SystemCurrentUserService.cs` (**solo sin seguridad**):
```csharp
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Services;

/// <summary>API sin autenticación: no hay usuario. La auditoría registra fechas y deja *By en null.</summary>
public sealed class SystemCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public Guid? UserId => null;
    public string? Email => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
```

`Infrastructure/Email/LoggingEmailSender.cs`
```csharp
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Email;

/// <summary>
/// Implementación de DESARROLLO: escribe el email en el log (incluye el enlace con el token).
/// En producción registrar una implementación real (SMTP con MailKit, SendGrid, etc.) con la misma interfaz.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL (dev) To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
```

## `Program.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using {Project};
using {Project}.Api.Middleware;
using {Project}.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Límites de Kestrel y sin header "Server" (standards/09).
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;   // 10 MB; subir solo en endpoints de archivos con [RequestSizeLimit]
});

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddSecurity()                           // [SEC]
    .AddApi(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
    app.UseForwardedHeaders();   // primero: el resto del pipeline (rate limit, logs, HTTPS) ve la IP y el esquema reales

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseTraceIdHeader();                      // X-Trace-Id en cada respuesta (standards/10)
app.UseSecurityHeaders();                    // nosniff, frame, referrer, CSP (standards/09)

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(DependencyInjection.CorsPolicy);
app.UseAuthentication();                     // [SEC]
app.UseRateLimiter();
app.UseAuthorization();                      // [SEC]

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });                      // proceso vivo
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });   // dependencias listas

// Migraciones al arrancar: solo en desarrollo y tests. En producción van en el pipeline (standards/13).
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await DatabaseSeeder.SeedAsync(app.Services);   // [SEC] roles, permisos y admin
await app.RunAsync();

public partial class Program;   // para WebApplicationFactory en tests de integración
```

## `appsettings.json` (sin secretos)
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database={Project}Db;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "{Project}.Api",
    "Audience": "{Project}.Client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "TwoFactorChallengeMinutes": 5
  },
  "App": {
    "ClientUrl": "https://localhost:5173",
    "AppName": "{Project}"
  },
  "ReverseProxy": {
    "Enabled": false,
    "KnownProxies": [],
    "KnownNetworks": []
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:5173" ]
  },
  "Database": {
    "ApplyMigrationsOnStartup": false
  },
  "Seed": {
    "AdminEmail": "admin@{project}.local"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```
Con PostgreSQL, la cadena de conexión cambia de formato: `"Default": "Host=localhost;Port=5432;Database={project}_db;Username={project}_app"` (la contraseña va en user-secrets: `dotnet user-secrets set "ConnectionStrings:Default" "Host=…;Password=…"`).

Sin seguridad se eliminan las secciones `Jwt`, `App` y `Seed`. `Database:ApplyMigrationsOnStartup` aplica a ambos perfiles (en `appsettings.Development.json` puede ir en `true`).

## Secretos de desarrollo y base de datos
```bash
dotnet user-secrets set "Jwt:SigningKey" "<cadena aleatoria de 64+ caracteres>"   # [SEC]
dotnet user-secrets set "Seed:AdminPassword" "<contraseña que cumpla la política>" # [SEC]
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate -o Infrastructure/Persistence/Migrations
dotnet ef database update
```
Para generar la llave en PowerShell: `[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }) -as [byte[]])`.
