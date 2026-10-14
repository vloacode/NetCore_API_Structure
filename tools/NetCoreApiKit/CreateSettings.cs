using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetCoreApiKit;

/// <summary>
/// Opciones del asistente. Cada pregunta tiene su flag: con todos los flags (o con --yes) no pregunta nada,
/// que es como lo usan la IA y la CI. Sin flags, pregunta paso a paso.
/// </summary>
public sealed class CreateSettings : CommandSettings
{
    [CommandOption("-n|--name <NOMBRE>")]
    [Description("Nombre del proyecto en PascalCase (ej. MiApi).")]
    public string? Name { get; init; }

    [CommandOption("--security")]
    [Description("API con usuarios: Identity local + JWT + 2FA + roles y permisos.")]
    public bool Security { get; init; }

    [CommandOption("--public")]
    [Description("API pública, sin usuarios.")]
    public bool Public { get; init; }

    [CommandOption("--apikey")]
    [Description("Solo API pública: proteger las escrituras con API key (X-Api-Key).")]
    public bool ApiKey { get; init; }

    [CommandOption("--database <MOTOR>")]
    [Description("postgresql | sqlserver")]
    public string? Database { get; init; }

    [CommandOption("--email <MODO>")]
    [Description("Solo con seguridad: smtp (MailKit + Mailpit) | log")]
    public string? Email { get; init; }

    [CommandOption("--analytics")]
    [Description("Agrega la analítica de uso del producto.")]
    public bool Analytics { get; init; }

    [CommandOption("--db-location <DONDE>")]
    [Description("docker | local | server | later")]
    public string? DbLocation { get; init; }

    [CommandOption("--connection <CADENA>")]
    [Description("Cadena de conexión para local/server. Mejor por la variable KIT_DB_CONNECTION (no queda en el historial).")]
    public string? Connection { get; init; }

    [CommandOption("--apply-migrations")]
    [Description("Crear las tablas en la base (por defecto: sí en docker/local, no en server).")]
    public bool ApplyMigrations { get; init; }

    [CommandOption("--no-apply-migrations")]
    public bool NoApplyMigrations { get; init; }

    [CommandOption("--secrets <DESTINOS>")]
    [Description("Dónde guardar lo sensible: user-secrets,env,appsettings (uno o varios). Por defecto: user-secrets.")]
    public string? SecretStores { get; init; }

    [CommandOption("--persist-env")]
    [Description("Con --secrets env en Windows: guardar también como variables de entorno del usuario.")]
    public bool PersistEnv { get; init; }

    [CommandOption("--git")]
    public bool Git { get; init; }

    [CommandOption("--no-git")]
    public bool NoGit { get; init; }

    [CommandOption("-o|--output <CARPETA>")]
    [Description("Carpeta destino (por defecto ./<nombre>).")]
    public string? Output { get; init; }

    [CommandOption("-y|--yes")]
    [Description("No preguntar: usar los valores por defecto para lo que no venga en flags.")]
    public bool Yes { get; init; }

    [CommandOption("--template-hive <CARPETA>", IsHidden = true)]
    public string? TemplateHive { get; init; }

    public override ValidationResult Validate()
    {
        if (Security && Public)
            return ValidationResult.Error("Usa --security o --public, no ambos.");
        if (Database is not null and not ("postgresql" or "sqlserver"))
            return ValidationResult.Error("--database debe ser postgresql o sqlserver.");
        if (Email is not null and not ("smtp" or "log"))
            return ValidationResult.Error("--email debe ser smtp o log.");
        if (DbLocation is not null and not ("docker" or "local" or "server" or "later"))
            return ValidationResult.Error("--db-location debe ser docker, local, server o later.");
        if (SecretStores is not null && SecretStores.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s is not ("user-secrets" or "env" or "appsettings")))
            return ValidationResult.Error("--secrets acepta user-secrets, env y appsettings (separados por coma).");
        if (ApplyMigrations && NoApplyMigrations)
            return ValidationResult.Error("Usa --apply-migrations o --no-apply-migrations, no ambos.");
        if (Git && NoGit)
            return ValidationResult.Error("Usa --git o --no-git, no ambos.");
        return ValidationResult.Success();
    }
}
