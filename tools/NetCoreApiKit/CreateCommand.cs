using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetCoreApiKit;

/// <summary>Asistente: pregunta lo que falte, muestra el resumen, genera el proyecto y lo deja listo para correr.</summary>
public sealed partial class CreateCommand : AsyncCommand<CreateSettings>
{
    private bool _interactive;
    private bool _yes;

    public override async Task<int> ExecuteAsync(CommandContext context, CreateSettings settings, CancellationToken cancellationToken)
    {
        _yes = settings.Yes;
        _interactive = !_yes && AnsiConsole.Profile.Capabilities.Interactive && !Console.IsInputRedirected;

        AnsiConsole.Write(new Rule($"[bold]NetCoreApi Kit {ProjectGenerator.KitVersion}[/] — crear una API ASP.NET Core").LeftJustified());
        var (sdkOk, efOk) = await Prerequisites.CheckAsync();
        if (!sdkOk)
            return 1;

        ProjectChoices choices;
        try
        {
            choices = await AskAsync(settings);
        }
        catch (StepFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 2;
        }

        ShowSummary(choices);
        if (_interactive && !AnsiConsole.Confirm("¿Crear el proyecto?", true))
            return 0;

        if (!efOk)
        {
            if (_yes || !_interactive || AnsiConsole.Confirm("Falta [bold]dotnet-ef[/] (migraciones). ¿Lo instalo? (dotnet tool install -g dotnet-ef)", true))
                efOk = await Prerequisites.InstallEfAsync();
            if (!efOk)
                AnsiConsole.MarkupLine("[yellow]Sin dotnet-ef no se crea la migración inicial; puedes hacerlo después.[/]");
        }

        try
        {
            var result = await RunAsync(choices, efOk);
            ShowNextSteps(choices, result);
            return 0;
        }
        catch (StepFailedException ex)
        {
            AnsiConsole.MarkupLine($"[red]✘ {Markup.Escape(ex.Message)}[/]");
            if (!string.IsNullOrWhiteSpace(ex.Details))
                AnsiConsole.Write(new Panel(Markup.Escape(ex.Details)).Header("detalle").BorderColor(Color.Red));
            return 1;
        }
    }

    // ------------------------------------------------------------ preguntas

    private async Task<ProjectChoices> AskAsync(CreateSettings s)
    {
        var name = s.Name ?? Ask(() => AnsiConsole.Prompt(new TextPrompt<string>("Nombre del proyecto (PascalCase, ej. [green]MiApi[/]):")
            .Validate(v => ValidName(v) ? ValidationResult.Success() : ValidationResult.Error("Usa PascalCase: letras y números, empieza con mayúscula."))), "--name");
        if (!ValidName(name))
            throw new StepFailedException($"Nombre inválido: '{name}'. Usa PascalCase (letras y números, empieza con mayúscula).");

        var output = Path.GetFullPath(s.Output ?? Path.Combine(Environment.CurrentDirectory, name));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any() && !File.Exists(Path.Combine(output, "AGENTS.md")))
            throw new StepFailedException($"La carpeta '{output}' ya existe y no está vacía. Elige otro nombre o --output.");

        var security = s.Security || (!s.Public && Choose("Tipo de API:", true,
            ("Con seguridad (usuarios, login JWT, 2FA, roles y permisos)", true),
            ("Pública (sin usuarios)", false)));

        var apiKey = !security && (s.ApiKey || Confirm("¿Proteger las escrituras (crear, editar, borrar) con API key?", false, forcedFalse: false));

        var database = s.Database ?? Choose("Base de datos:", "postgresql", ("PostgreSQL", "postgresql"), ("SQL Server", "sqlserver"));

        var email = !security ? "log" : s.Email ?? Choose("Emails (confirmación de cuenta, reset de contraseña):", "smtp",
            ("SMTP real (con Mailpit para probar en local)", "smtp"), ("Solo al log (desarrollo)", "log"));

        var analytics = s.Analytics || (!_yes && _interactive && AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title("Capacidades extra (espacio para marcar, enter para seguir):")
                .NotRequired()
                .AddChoices("Analítica de uso del producto"))
            .Count > 0);

        var location = ParseLocation(s.DbLocation) ?? Choose("¿Dónde está la base de datos?", DbLocation.Docker,
            ("Docker (te creo un contenedor local)", DbLocation.Docker),
            ("Instalada en esta máquina", DbLocation.Local),
            ("Un servidor (otra máquina, la nube, la base de la empresa)", DbLocation.Server),
            ("La configuro después", DbLocation.Later));

        string? connection = null;
        if (location is DbLocation.Local or DbLocation.Server)
        {
            connection = s.Connection ?? Environment.GetEnvironmentVariable("KIT_DB_CONNECTION");
            var defaultDb = database == "postgresql" ? $"{name.ToLowerInvariant()}_db" : $"{name}Db";
            if (connection is null)
            {
                if (!_interactive)
                    throw new StepFailedException("Falta la conexión: usa --connection \"<cadena>\" o la variable KIT_DB_CONNECTION.");
                connection = await DatabaseSetup.AskConnectionAsync(database, defaultDb, location == DbLocation.Server);
                if (connection is null)
                    location = DbLocation.Later;
            }
            else
            {
                var check = await DatabaseSetup.TestAsync(database, connection);
                if (!check.Ok)
                    throw new StepFailedException($"No se pudo conectar a la base: {check.Message}");
                AnsiConsole.MarkupLine($"  [green]✔[/] {Markup.Escape(check.Message)}");
            }
        }
        if (location == DbLocation.Docker && !await Prerequisites.DockerRunningAsync())
            throw new StepFailedException("Docker no está corriendo. Ábrelo (Docker Desktop) o elige otra ubicación para la base.");

        var applyMigrations = location switch
        {
            DbLocation.Docker => !s.NoApplyMigrations,
            DbLocation.Local => s.ApplyMigrations || (!s.NoApplyMigrations && Confirm("¿Crear la base y las tablas ahora?", true, s.NoApplyMigrations)),
            DbLocation.Server => s.ApplyMigrations || (!s.NoApplyMigrations && Confirm(
                "¿Crear las tablas en el servidor ahora? (si es una base compartida, mejor no)", false, s.NoApplyMigrations)),
            _ => false
        };

        var stores = ParseStores(s.SecretStores) ?? (_interactive
            ? AnsiConsole.Prompt(new MultiSelectionPrompt<SecretStore>()
                    .Title("¿Dónde guardo la configuración sensible? (conexión, llave JWT, contraseña del admin…)")
                    .InstructionsText("[grey](espacio para marcar uno o varios, enter para seguir)[/]")
                    .UseConverter(StoreLabel)
                    .AddChoices(SecretStore.UserSecrets, SecretStore.Environment, SecretStore.AppSettingsLocal)
                    .Select(SecretStore.UserSecrets))
                .ToHashSet()
            : [SecretStore.UserSecrets]);
        if (stores.Count == 0)
            stores = [SecretStore.UserSecrets];

        var persistEnv = s.PersistEnv || (stores.Contains(SecretStore.Environment) && OperatingSystem.IsWindows() && _interactive
            && AnsiConsole.Confirm("  ¿Guardarlas también como variables de entorno de tu usuario de Windows? (modifica tu entorno)", false));

        var git = !s.NoGit && (s.Git || Confirm("¿Inicializar git?", true, false));

        return new ProjectChoices
        {
            Name = name, OutputDirectory = output, Security = security, ApiKey = apiKey, Database = database, Email = email,
            Analytics = analytics, DbLocation = location, ConnectionString = connection, ApplyMigrations = applyMigrations,
            SecretStores = stores, PersistEnvironment = persistEnv, Git = git, TemplateHive = s.TemplateHive
        };
    }

    private T Ask<T>(Func<T> prompt, string flag)
    {
        if (!_interactive)
            throw new StepFailedException($"Falta {flag} (la consola no es interactiva o se usó --yes).");
        return prompt();
    }

    private T Choose<T>(string title, T defaultValue, params (string Label, T Value)[] options) where T : notnull
    {
        if (!_interactive)
            return defaultValue;
        var labels = options.ToDictionary(o => o.Label, o => o.Value);
        var ordered = options.OrderByDescending(o => EqualityComparer<T>.Default.Equals(o.Value, defaultValue)).Select(o => o.Label);
        return labels[AnsiConsole.Prompt(new SelectionPrompt<string>().Title(title).AddChoices(ordered))];
    }

    private bool Confirm(string question, bool defaultValue, bool forcedFalse)
        => !forcedFalse && (_interactive ? AnsiConsole.Confirm(question, defaultValue) : defaultValue);

    private static bool ValidName(string value) => PascalCaseRegex().IsMatch(value);

    private static DbLocation? ParseLocation(string? value) => value switch
    {
        "docker" => DbLocation.Docker,
        "local" => DbLocation.Local,
        "server" => DbLocation.Server,
        "later" => DbLocation.Later,
        _ => null
    };

    private static HashSet<SecretStore>? ParseStores(string? value) => value?
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v switch { "env" => SecretStore.Environment, "appsettings" => SecretStore.AppSettingsLocal, _ => SecretStore.UserSecrets })
        .ToHashSet();

    private static string StoreLabel(SecretStore store) => store switch
    {
        SecretStore.UserSecrets => "User-secrets (recomendado en desarrollo)",
        SecretStore.Environment => "Variables de entorno",
        _ => "appsettings local (appsettings.Development.local.json, no se sube a git)"
    };

    // ------------------------------------------------------------ resumen y ejecución

    private static void ShowSummary(ProjectChoices c)
    {
        var table = new Table().Border(TableBorder.Rounded).HideHeaders().AddColumn("").AddColumn("");
        table.AddRow("Proyecto", $"[bold]{c.Name}[/] → {Markup.Escape(c.OutputDirectory)}");
        table.AddRow("Tipo", c.Security ? "Con seguridad (Identity + JWT + 2FA + permisos)" : c.ApiKey ? "Pública con API key en escrituras" : "Pública");
        table.AddRow("Base de datos", $"{(c.PostgreSql ? "PostgreSQL" : "SQL Server")} — {LocationLabel(c.DbLocation)}{(c.ApplyMigrations ? ", se crean las tablas" : "")}");
        if (c.Security)
            table.AddRow("Emails", c.Smtp ? "SMTP (Mailpit en local)" : "Al log");
        table.AddRow("Extras", c.Analytics ? "Analítica de uso" : "—");
        table.AddRow("Secretos", string.Join(", ", c.SecretStores.Select(StoreLabel)));
        if (c.SecretStores.Count > 1)
            table.AddRow("", "[grey]Si un valor está en varios lados gana: variables de entorno > user-secrets > appsettings local[/]");
        table.AddRow("Guía para IA", "Sí (AGENTS.md, ai/, standards/, docs/)");
        table.AddRow("Git", c.Git ? "git init" : "No");
        AnsiConsole.Write(table);
    }

    private static string LocationLabel(DbLocation location) => location switch
    {
        DbLocation.Docker => "contenedor Docker local",
        DbLocation.Local => "instalada en esta máquina",
        DbLocation.Server => "servidor",
        _ => "se configura después"
    };

    private sealed record RunResult(string? AdminPassword, IReadOnlyList<string> SecretsWrittenTo, bool MigrationApplied, bool Git);

    private static async Task<RunResult> RunAsync(ProjectChoices c, bool efOk)
    {
        string? admin = null;
        IReadOnlyList<string> written = [];
        var migrated = false;
        var git = false;

        await AnsiConsole.Status().StartAsync("Instalando plantillas…", async ctx =>
        {
            await ProjectGenerator.InstallTemplatesAsync(c);
            Done("Plantillas instaladas (dotnet new kitapi / kit-entity)");

            ctx.Status("Generando el proyecto…");
            await ProjectGenerator.GenerateAsync(c);
            ProjectGenerator.CopyGuide(c);
            await ProjectGenerator.FillProfileAsync(c);
            await ProjectGenerator.WriteReadmeAsync(c);
            Done("Proyecto generado con la guía para IA y el perfil lleno");

            var secrets = LocalSetup.BuildSecrets(c, out admin);
            if (c.DbLocation == DbLocation.Docker)
            {
                c.ConnectionString = await LocalSetup.StartDockerDatabaseAsync(c, secrets, msg => ctx.Status(msg));
                Done("Base de datos en Docker lista");
            }
            if (c.ConnectionString is not null)
                secrets["ConnectionStrings:Default"] = c.ConnectionString;

            ctx.Status("Guardando la configuración sensible…");
            written = await ConfigStore.SaveAsync(c, secrets);
            if (written.Count > 0)
                Done("Secretos guardados en: " + string.Join("; ", written));

            ctx.Status("Formateando y compilando…");
            await LocalSetup.FormatAndBuildAsync(c);
            Done("Compilado");

            if (efOk)
            {
                ctx.Status("Creando la migración inicial…");
                await LocalSetup.AddInitialMigrationAsync(c);
                Done("Migración InitialCreate creada");

                if (c.ApplyMigrations && c.ConnectionString is not null)
                {
                    ctx.Status("Creando las tablas en la base…");
                    await LocalSetup.ApplyMigrationsAsync(c);
                    migrated = true;
                    Done("Tablas creadas");
                }
            }

            if (c.Git)
            {
                git = await LocalSetup.InitGitAsync(c);
                if (git) Done("Repositorio git inicializado");
            }
        });
        return new RunResult(admin, written, migrated, git);
    }

    private static void Done(string message) => AnsiConsole.MarkupLine($"  [green]✔[/] {Markup.Escape(message)}");

    private static void ShowNextSteps(ProjectChoices c, RunResult r)
    {
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, c.OutputDirectory);
        var lines = new List<string>
        {
            "[bold]Siguientes pasos[/]",
            $"  cd {Markup.Escape(relative)}",
            $"  dotnet run --project src/{c.Name}.Api      → https://localhost:7100/scalar"
        };
        if (r.AdminPassword is not null)
            lines.Add($"  Admin: admin@{c.LowerName}.local / [yellow]{Markup.Escape(r.AdminPassword)}[/]  [grey](guárdala: no se vuelve a mostrar)[/]");
        if (c.Smtp)
            lines.Add("  Emails de prueba: http://localhost:8025 (Mailpit)");
        if (c.ConnectionString is null)
            lines.Add("  [yellow]Base pendiente:[/] guarda ConnectionStrings:Default (ver README) y corre dotnet ef database update -p src/" + c.Name + ".Api");
        else if (!r.MigrationApplied)
            lines.Add($"  [yellow]Tablas pendientes:[/] dotnet ef database update -p src/{c.Name}.Api  (cuando sea el momento en ese servidor)");
        if (c.SecretStores.Contains(SecretStore.Environment) && !c.PersistEnvironment)
            lines.Add("  Variables de entorno: cárgalas antes de correr con  . ./env.local.ps1  (o  source ./env.local.sh)");
        if (c.DbLocation == DbLocation.Server)
            lines.Add("  En producción, la conexión va en la variable ConnectionStrings__Default o en Key Vault (standards/13).");
        lines.Add("");
        lines.Add("  [bold]¿Con IA?[/] Abre la carpeta y escribe /project-init (o \"lee AGENTS.md\"). Seguirá con la entrevista y las entidades.");
        lines.Add($"  Nueva entidad a mano: dotnet new kit-entity -n <Entidad> --app {c.Name} --security {ProjectGenerator.Bool(c.Security)} --database {c.Database}{(c.ApiKey ? " --apikey true" : "")}");
        AnsiConsole.Write(new Panel(string.Join("\n", lines)).Header("[green]Listo[/]").BorderColor(Color.Green));
    }

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex PascalCaseRegex();
}
