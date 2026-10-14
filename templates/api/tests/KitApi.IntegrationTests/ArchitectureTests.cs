using System.Reflection;
#if (security)
using Microsoft.AspNetCore.Authorization;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Common.Results;
using KitApi.Domain.Common;
using KitApi.Infrastructure.Persistence;

namespace KitApi.IntegrationTests;

/// <summary>
/// Reglas de oro del kit (AGENTS.md, standards/01) comprobadas sobre el código real. Si una entidad o un service
/// nuevo se aparta del molde, estos tests fallan con la lista de lo que hay que corregir. No usan base de datos.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly _api = typeof(Program).Assembly;
    private const string FeaturesNamespace = "KitApi.Features";

    private static IEnumerable<Type> ConcreteTypes => _api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

    [Fact]
    public static void Controllers_DoNotDependOnPersistence()
    {
        var forbidden = new[] { typeof(DbContext), typeof(IUnitOfWork), typeof(IRepository<>) };

        var violations = ConcreteTypes
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => (Controller: t, p.ParameterType)))
            .Where(x => forbidden.Any(f => f.IsAssignableFrom(x.ParameterType)
                                           || (x.ParameterType.IsGenericType && x.ParameterType.GetGenericTypeDefinition() == f)))
            .Select(x => $"{x.Controller.Name} recibe {x.ParameterType.Name}: un controller solo llama a services");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public static void FeatureServices_ImplementTheirInterface()
    {
        var violations = ConcreteTypes
            .Where(t => t.Namespace?.StartsWith(FeaturesNamespace, StringComparison.Ordinal) == true
                        && t.Name.EndsWith("Service", StringComparison.Ordinal)
                        && t.GetInterface("I" + t.Name) is null)
            .Select(t => $"{t.Name} no implementa I{t.Name}: AddApplication() no lo registraría");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public static void FeatureServiceMethods_ReturnResult()
    {
        var violations = ConcreteTypes
            .Where(t => t.Namespace?.StartsWith(FeaturesNamespace, StringComparison.Ordinal) == true)
            .Select(t => t.GetInterface("I" + t.Name))
            .OfType<Type>()
            .SelectMany(i => i.GetMethods().Select(m => (Interface: i, Method: m)))
            .Where(x => !ReturnsResult(x.Method.ReturnType))
            .Select(x => $"{x.Interface.Name}.{x.Method.Name} devuelve {x.Method.ReturnType.Name}: debe devolver Task<Result> o Task<Result<T>>");

        violations.ShouldBeEmpty();
    }

    [Fact]
    public static void Entities_HaveDbSetAndConfiguration()
    {
        var dbSets = typeof(AppDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .ToHashSet();
        var configured = ConcreteTypes
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToHashSet();

        var violations = ConcreteTypes
            .Where(t => t.IsSubclassOf(typeof(BaseEntity)))
            .SelectMany(t => new[]
            {
                dbSets.Contains(t) ? null : $"{t.Name} no tiene DbSet en AppDbContext (copiar el comentario REGISTRO)",
                configured.Contains(t) ? null : $"{t.Name} no tiene IEntityTypeConfiguration"
            })
            .OfType<string>();

        violations.ShouldBeEmpty();
    }
#if (security)

    [Fact]
    public static void FeatureControllerActions_DeclareAuthorization()
    {
        var violations = ConcreteTypes
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t)
                        && t.Namespace?.StartsWith(FeaturesNamespace, StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => (Controller: t, Action: m)))
            .Where(x => !x.Action.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any()
                        && !x.Action.GetCustomAttributes(true).OfType<IAuthorizeData>().Any()
                        && !x.Controller.GetCustomAttributes(true).OfType<IAuthorizeData>().Any())
            .Select(x => $"{x.Controller.Name}.{x.Action.Name} no tiene [HasPermission] (ni [AllowAnonymous] explícito)");

        violations.ShouldBeEmpty();
    }
#endif

    private static bool ReturnsResult(Type type)
        => type.IsGenericType
           && type.GetGenericTypeDefinition() == typeof(Task<>)
           && typeof(Result).IsAssignableFrom(type.GetGenericArguments()[0]);
}
