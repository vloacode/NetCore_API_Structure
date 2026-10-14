using Microsoft.EntityFrameworkCore;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Abstractions.Services;
using KitApi.Infrastructure.Email;
#if (analytics)
using KitApi.Infrastructure.Analytics;
#endif
using KitApi.Infrastructure.Persistence;
using KitApi.Infrastructure.Persistence.Interceptors;
#if (!security)
using KitApi.Infrastructure.Services;
#endif

namespace KitApi.Extensions;

public static class PersistenceExtensions
{
    /// <summary>EF Core + Unit of Work, auditoría, health check de la BD y servicios de infraestructura.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
#if (sqlserver)
            .UseSqlServer(configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure())
#endif
#if (postgresql)
            .UseNpgsql(configuration.GetConnectionString("Default"), npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention()
#endif
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // /health/ready verifica la base de datos (standards/10).
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

#if (smtpEmail)
        // Email real por SMTP (MailKit). Desarrollo: Mailpit en docker-compose (http://localhost:8025).
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
#else
        // Email de desarrollo: se escribe en el log. Para emails reales, generar con --email smtp (standards/09).
        services.AddScoped<IEmailSender, LoggingEmailSender>();
#endif

#if (analytics)
        services.AddProductAnalytics(configuration);   // analítica de uso (standards/17)

#endif
#if (!security)
        // Sin seguridad no hay usuario autenticado: la auditoría guarda solo fechas.
        services.AddScoped<ICurrentUserService, SystemCurrentUserService>();

#endif
        return services;
    }
}
