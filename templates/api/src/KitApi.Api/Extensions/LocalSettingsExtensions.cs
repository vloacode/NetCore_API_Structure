using Microsoft.Extensions.Configuration.Json;

namespace KitApi.Extensions;

public static class LocalSettingsExtensions
{
    /// <summary>
    /// Carga <c>appsettings.{Entorno}.local.json</c> (opcional, ignorado por git) justo después de <c>appsettings.{Entorno}.json</c>.
    /// Orden resultante (el último gana): appsettings.json → appsettings.{Env}.json → local → user-secrets → variables de entorno → argumentos.
    /// Sirve para guardar configuración de la máquina (incluidos secretos de desarrollo) sin subirla al repositorio.
    /// </summary>
    public static WebApplicationBuilder AddLocalSettings(this WebApplicationBuilder builder)
    {
        var environment = builder.Environment.EnvironmentName;
        var sources = builder.Configuration.Sources;

        var local = new JsonConfigurationSource
        {
            Path = $"appsettings.{environment}.local.json",
            Optional = true,
            ReloadOnChange = true,
            FileProvider = builder.Environment.ContentRootFileProvider
        };

        var environmentJson = sources
            .Select((source, index) => (source, index))
            .FirstOrDefault(s => s.source is JsonConfigurationSource json
                                 && string.Equals(json.Path, $"appsettings.{environment}.json", StringComparison.OrdinalIgnoreCase));

        if (environmentJson.source is null)
            sources.Add(local);
        else
            sources.Insert(environmentJson.index + 1, local);

        return builder;
    }
}
