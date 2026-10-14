using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NetCoreApiKit;

/// <summary>
/// Guarda la configuración sensible ("Clave:Subclave" → valor) en los destinos elegidos.
/// Nunca escribe en appsettings.json ni en archivos que se suben a git.
/// </summary>
public static partial class ConfigStore
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static async Task<IReadOnlyList<string>> SaveAsync(ProjectChoices choices, IReadOnlyDictionary<string, string> values)
    {
        var written = new List<string>();
        if (values.Count == 0)
            return written;

        if (choices.SecretStores.Contains(SecretStore.UserSecrets))
            written.Add(await WriteUserSecretsAsync(choices, values));

        if (choices.SecretStores.Contains(SecretStore.AppSettingsLocal))
            written.Add(await WriteLocalAppSettingsAsync(choices, values));

        if (choices.SecretStores.Contains(SecretStore.Environment))
        {
            written.Add(await WriteEnvironmentScriptsAsync(choices, values));
            if (choices.PersistEnvironment && OperatingSystem.IsWindows())
            {
                foreach (var (key, value) in values)
                    Environment.SetEnvironmentVariable(ToEnvironmentName(key), value, EnvironmentVariableTarget.User);
                written.Add("variables de entorno de tu usuario de Windows (abre una terminal nueva para verlas)");
            }
        }
        return written;
    }

    /// <summary>"ConnectionStrings:Default" → "ConnectionStrings__Default" (convención de ASP.NET Core).</summary>
    public static string ToEnvironmentName(string key) => key.Replace(":", "__", StringComparison.Ordinal);

    /// <summary>secrets.json del proyecto (mismo formato que <c>dotnet user-secrets set</c>), fusionando lo que ya exista.</summary>
    private static async Task<string> WriteUserSecretsAsync(ProjectChoices choices, IReadOnlyDictionary<string, string> values)
    {
        var csproj = await File.ReadAllTextAsync(Path.Combine(choices.ApiProjectDirectory, $"{choices.Name}.Api.csproj"));
        var id = UserSecretsIdRegex().Match(csproj).Groups[1].Value;
        var root = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets");
        var file = Path.Combine(root, id, "secrets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var existing = File.Exists(file)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(file)) ?? []
            : new Dictionary<string, string>();
        foreach (var (key, value) in values)
            existing[key] = value;
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(existing, Indented));
        return $"user-secrets ({id})";
    }

    /// <summary>appsettings.Development.local.json: lo carga AddLocalSettings() y está en .gitignore.</summary>
    private static async Task<string> WriteLocalAppSettingsAsync(ProjectChoices choices, IReadOnlyDictionary<string, string> values)
    {
        var file = Path.Combine(choices.ApiProjectDirectory, "appsettings.Development.local.json");
        var root = File.Exists(file) ? JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject() : new JsonObject();
        foreach (var (key, value) in values)
        {
            var parts = key.Split(':');
            var node = root;
            foreach (var part in parts[..^1])
                node = node[part] as JsonObject ?? (JsonObject)(node[part] = new JsonObject());
            node[parts[^1]] = value;
        }
        await File.WriteAllTextAsync(file, root.ToJsonString(Indented));
        return Path.GetRelativePath(choices.OutputDirectory, file) + " (no se sube a git)";
    }

    /// <summary>env.local.ps1 / env.local.sh: cargan las variables en la terminal actual. Ignorados por git.</summary>
    private static async Task<string> WriteEnvironmentScriptsAsync(ProjectChoices choices, IReadOnlyDictionary<string, string> values)
    {
        var ps1 = new StringBuilder("# Variables de entorno de desarrollo (no se sube a git). Uso: . ./env.local.ps1\n");
        var sh = new StringBuilder("# Variables de entorno de desarrollo (no se sube a git). Uso: source ./env.local.sh\n");
        foreach (var (key, value) in values)
        {
            var name = ToEnvironmentName(key);
            ps1.Append(CultureInvariant($"$env:{name} = '{value.Replace("'", "''", StringComparison.Ordinal)}'\n"));
            sh.Append(CultureInvariant($"export {name}='{value.Replace("'", "'\\''", StringComparison.Ordinal)}'\n"));
        }
        await File.WriteAllTextAsync(Path.Combine(choices.OutputDirectory, "env.local.ps1"), ps1.ToString());
        await File.WriteAllTextAsync(Path.Combine(choices.OutputDirectory, "env.local.sh"), sh.ToString());
        return "env.local.ps1 / env.local.sh (no se suben a git)";
    }

    private static string CultureInvariant(FormattableString value) => FormattableString.Invariant(value);

    [GeneratedRegex("<UserSecretsId>([^<]+)</UserSecretsId>")]
    private static partial Regex UserSecretsIdRegex();
}
