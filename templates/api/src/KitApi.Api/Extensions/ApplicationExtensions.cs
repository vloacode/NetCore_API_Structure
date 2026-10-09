using System.Reflection;
using FluentValidation;

namespace KitApi.Extensions;

public static class ApplicationExtensions
{
    /// <summary>Namespace de las carpetas Features/{Entities}: solo ahí se registran services automáticamente.</summary>
    private const string FeaturesNamespace = "KitApi.Features";

    /// <summary>
    /// Validadores de FluentValidation y services de negocio. Cada clase <c>{Entity}Service</c> de Features/
    /// que implemente <c>I{Entity}Service</c> se registra como scoped: una entidad nueva no toca este archivo.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

        var serviceTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Service", StringComparison.Ordinal)
                        && t.Namespace?.StartsWith(FeaturesNamespace, StringComparison.Ordinal) == true);

        foreach (var implementation in serviceTypes)
        {
            var contract = implementation.GetInterface("I" + implementation.Name);
            if (contract is not null)
                services.AddScoped(contract, implementation);
        }

        return services;
    }
}
