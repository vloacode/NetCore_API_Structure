using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace KitApi.Api.Authentication;

public sealed class ApiKeyOptions : AuthenticationSchemeOptions
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public sealed record ApiClient(string ClientId, string Name);

public interface IApiKeyStore
{
    ValueTask<ApiClient?> ValidateAsync(string rawKey, CancellationToken ct);
}

/// <summary>Claves en configuración (user-secrets / Key Vault), guardadas como hash SHA-256 en hex.</summary>
public sealed class ConfigurationApiKeyStore(IConfiguration configuration) : IApiKeyStore
{
    private sealed record Entry(string ClientId, string Name, string KeyHash);

    public ValueTask<ApiClient?> ValidateAsync(string rawKey, CancellationToken ct)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var entries = configuration.GetSection("ApiKeys:Clients").Get<Entry[]>() ?? [];

        foreach (var entry in entries)
            if (CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(entry.KeyHash)))
                return ValueTask.FromResult<ApiClient?>(new ApiClient(entry.ClientId, entry.Name));

        return ValueTask.FromResult<ApiClient?>(null);
    }
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyOptions> options, ILoggerFactory logger, UrlEncoder encoder, IApiKeyStore store)
    : AuthenticationHandler<ApiKeyOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var value) || string.IsNullOrWhiteSpace(value))
            return AuthenticateResult.NoResult();

        var client = await store.ValidateAsync(value.ToString(), Context.RequestAborted);
        if (client is null)
            return AuthenticateResult.Fail("API key inválida.");

        var identity = new ClaimsIdentity(
            [new Claim("sub", client.ClientId), new Claim("client_name", client.Name)], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

public static class ApiKeyExtensions
{
    /// <summary>API key en el header X-Api-Key (perfil sin seguridad). Uso: [Authorize] en las acciones de escritura.</summary>
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IApiKeyStore, ConfigurationApiKeyStore>();
        services.AddAuthentication(ApiKeyOptions.Scheme)
            .AddScheme<ApiKeyOptions, ApiKeyAuthenticationHandler>(ApiKeyOptions.Scheme, null);
        services.AddAuthorization();
        return services;
    }
}
