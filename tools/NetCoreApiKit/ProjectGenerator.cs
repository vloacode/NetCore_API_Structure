using System.Text.RegularExpressions;

namespace NetCoreApiKit;

public sealed class StepFailedException(string message, string? details = null) : Exception(message)
{
    public string? Details { get; } = details;
}

/// <summary>Genera la solución con las plantillas del kit y le agrega la guía para IA.</summary>
public static partial class ProjectGenerator
{
    /// <summary>Carpeta del kit que viaja dentro de la herramienta (plantillas + guía).</summary>
    public static string KitDirectory => Path.Combine(AppContext.BaseDirectory, "kit");

    public static string KitVersion =>
        File.Exists(Path.Combine(KitDirectory, "KIT_VERSION")) ? File.ReadAllText(Path.Combine(KitDirectory, "KIT_VERSION")).Trim() : "0.0.0";

    /// <summary>Elementos de la guía que se copian al proyecto. Lo propio del kit (templates/, scripts/verify/, su CI) no.</summary>
    private static readonly string[] GuideEntries =
    [
        "AGENTS.md", "CLAUDE.md", "KIT_VERSION", ".mcp.json", "ai", "standards", "docs", ".claude", ".cursor",
        Path.Combine("scripts", "hooks"), Path.Combine("scripts", "check-kit.py"),
        Path.Combine(".github", "copilot-instructions.md"), Path.Combine(".vscode", "mcp.json")
    ];

    public static async Task InstallTemplatesAsync(ProjectChoices choices)
    {
        foreach (var template in new[] { "api", "entity" })
        {
            var result = await Shell.RunAsync("dotnet",
                ["new", "install", Path.Combine(KitDirectory, "templates", template), "--force", .. HiveArgs(choices)]);
            if (!result.Ok)
                throw new StepFailedException($"No se pudo instalar la plantilla '{template}'.", result.Tail());
        }
    }

    public static async Task GenerateAsync(ProjectChoices choices)
    {
        var intoExistingKit = File.Exists(Path.Combine(choices.OutputDirectory, "AGENTS.md"));
        string[] args =
        [
            "new", "kitapi", "-n", choices.Name, "-o", choices.OutputDirectory,
            "--security", Bool(choices.Security), "--database", choices.Database, "--analytics", Bool(choices.Analytics),
            "--apikey", Bool(choices.ApiKey), "--email", choices.Smtp ? "smtp" : "log", "--framework", "net10.0",
            .. intoExistingKit ? new[] { "--force" } : [],
            .. HiveArgs(choices)
        ];
        var result = await Shell.RunAsync("dotnet", args);
        if (!result.Ok)
            throw new StepFailedException("dotnet new kitapi falló.", result.Tail());
    }

    /// <summary>Copia la guía para IA. Si la carpeta ya es un repo del kit (tiene AGENTS.md), no toca nada.</summary>
    public static void CopyGuide(ProjectChoices choices)
    {
        foreach (var entry in GuideEntries)
        {
            var source = Path.Combine(KitDirectory, entry);
            var target = Path.Combine(choices.OutputDirectory, entry);
            if (File.Exists(source))
            {
                if (!File.Exists(target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                }
            }
            else if (Directory.Exists(source))
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                    if (File.Exists(destination))
                        continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination);
                }
            }
        }
    }

    /// <summary>Llena el Perfil del proyecto en docs/00-MASTER_CONTEXT.md con las respuestas (lo lee project-init y toda IA).</summary>
    public static async Task FillProfileAsync(ProjectChoices choices)
    {
        var file = Path.Combine(choices.OutputDirectory, "docs", "00-MASTER_CONTEXT.md");
        if (!File.Exists(file))
            return;

        var text = await File.ReadAllTextAsync(file);
        var profile = new Dictionary<string, string>
        {
            ["KitVersion"] = KitVersion,
            ["Mode"] = choices.Security ? "1-standard-secure" : "2-standard-public",
            ["Security"] = choices.Security ? "enabled" : "disabled",
            ["TwoFactor"] = choices.Security ? "enabled" : "disabled",
            ["PublicRegistration"] = choices.Security ? "enabled" : "disabled",
            ["ApiKey"] = choices.ApiKey && !choices.Security ? "enabled" : "disabled",
            ["PrimaryKey"] = "int",
            ["SoftDelete"] = "enabled",
            ["Database"] = choices.Database,
            ["TargetFramework"] = "net10.0",
            ["Email"] = choices.Smtp ? "smtp" : "log",
            ["Deployment"] = "docker",
            ["FrontendOrigins"] = "https://localhost:5173",
            ["Capabilities"] = choices.Analytics ? "product-analytics" : "ninguna"
        };
        foreach (var (key, value) in profile)
            text = Regex.Replace(text, $@"^\| {key} \| \{{\{{[^}}]*\}}\}} \|", $"| {key} | {value} |", RegexOptions.Multiline);
        text = text.Replace("{{NombreProyecto}}", choices.Name, StringComparison.Ordinal);
        await File.WriteAllTextAsync(file, text);
    }

    /// <summary>README corto del proyecto: cómo correrlo. El detalle está en AGENTS.md y standards/.</summary>
    public static async Task WriteReadmeAsync(ProjectChoices choices)
    {
        var file = Path.Combine(choices.OutputDirectory, "README.md");
        if (File.Exists(file))
            return;

        var api = $"src/{choices.Name}.Api";
        var lines = new List<string>
        {
            $"# {choices.Name}",
            "",
            $"API ASP.NET Core (.NET 10) creada con el kit NetCore_API_Structure {KitVersion}.",
            "",
            "## Correr",
            "```bash",
            $"dotnet run --project {api}",
            "```",
            "- Documentación interactiva: `https://localhost:7100/scalar`",
            "- Salud: `/health/live` y `/health/ready`"
        };
        if (choices.Security)
            lines.Add($"- Admin inicial: `admin@{choices.LowerName}.local` (contraseña en tus secretos de desarrollo: `Seed:AdminPassword`)");
        if (choices.Smtp)
            lines.Add("- Emails de prueba (Mailpit): `docker compose up -d mail` → `http://localhost:8025`");
        lines.AddRange(
        [
            "",
            "## Base de datos y configuración",
            "- Cadena de conexión: `ConnectionStrings:Default`, en user-secrets, variables de entorno (`ConnectionStrings__Default`) o `appsettings.Development.local.json` (no se sube a git).",
            "- Base local con Docker: `docker compose up -d db` (claves y puerto en `.env`).",
            $"- Migraciones: `dotnet ef migrations add <Nombre> -p {api} -o Infrastructure/Persistence/Migrations` y `dotnet ef database update -p {api}`.",
            "",
            "## Comandos",
            "```bash",
            "dotnet build",
            "dotnet test",
            $"dotnet new kit-entity -n <Entidad> --app {choices.Name} --security {Bool(choices.Security)} --database {choices.Database}{(choices.ApiKey ? " --apikey true" : "")}",
            "```",
            "",
            "## Trabajar con IA",
            "Abre la carpeta con tu IA y pide `/project-init` (Claude Code) o \"lee AGENTS.md y ejecuta ai/workflows/project-init.md\".",
            "El proyecto ya está generado: la IA sigue con la entrevista del negocio, la documentación y las entidades, fase por fase."
        ]);
        await File.WriteAllTextAsync(file, string.Join("\n", lines) + "\n");
    }

    public static string Bool(bool value) => value ? "true" : "false";

    private static string[] HiveArgs(ProjectChoices choices)
        => choices.TemplateHive is null ? [] : ["--debug:custom-hive", choices.TemplateHive];
}
