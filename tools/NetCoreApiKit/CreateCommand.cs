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
            if (OperatingSystem.IsWindows() && ex.Details is { } details
                && (details.Contains("Application Control", StringComparison.OrdinalIgnoreCase)
                    || details.Contains("Could not load assembly", StringComparison.OrdinalIgnoreCase)))
                AnsiConsole.MarkupLine("  [yellow]Posible causa:[/] Windows (Smart App Control / control de aplicaciones) bloquea las DLL recién compiladas. " +
                                       "Corre el mismo comando de dotnet ef con [grey]-v[/]: si dice \"An Application Control policy has blocked this file\", " +
                                       "revísalo en Seguridad de Windows → Control de aplicaciones y navegador. El proyecto ya quedó generado.");
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

        // Validación previa: si la base ya tiene tablas, mostrar qué hay y decidir antes de generar nada.
        var existingDb = ExistingDbMode.None;
        if (connection is { } current)
        {
            (connection, existingDb) = await ResolveExistingDatabaseAsync(database, current, s);
            if (connection is null)
                location = DbLocation.Later;
        }

        var applyMigrations = existingDb switch
        {
            ExistingDbMode.Backup or ExistingDbMode.Reuse => true,   // el detalle se confirma antes de aplicar
            ExistingDbMode.Skip => false,
            _ => location switch
        {
            DbLocation.Docker => !s.NoApplyMigrations,
            DbLocation.Local => s.ApplyMigrations || (!s.NoApplyMigrations && Confirm("¿Crear la base y las tablas ahora?", true, s.NoApplyMigrations)),
            DbLocation.Server => s.ApplyMigrations || (!s.NoApplyMigrations && Confirm(
                "¿Crear las tablas en el servidor ahora? (si es una base compartida, mejor no)", false, s.NoApplyMigrations)),
            _ => false
        }
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
            Analytics = analytics, DbLocation = location, ConnectionString = connection, ApplyMigrations = applyMigrations, ExistingDb = existingDb,
            SecretStores = stores, PersistEnvironment = persistEnv, Git = git, TemplateHive = s.TemplateHive
        };
    }

    /// <summary>
    /// Si la base ya tiene tablas, muestra cuáles (Identity y otras, con filas) y pregunta qué hacer.
    /// Nunca se borra nada: o se respalda, o se reutiliza, o se usa otra base, o no se toca.
    /// </summary>
    private async Task<(string? Connection, ExistingDbMode Mode)> ResolveExistingDatabaseAsync(string database, string connection, CreateSettings s)
    {
        while (true)
        {
            var tables = await SchemaInspector.ListTablesAsync(database, connection);
            if (tables.Count == 0)
                return (connection, ExistingDbMode.None);

            var rows = await SchemaInspector.EstimateRowsAsync(database, connection);
            ShowExistingTables(tables, rows);

            var flag = s.ExistingDb switch
            {
                "backup" => ExistingDbMode.Backup,
                "reuse" => ExistingDbMode.Reuse,
                "skip" => ExistingDbMode.Skip,
                _ => (ExistingDbMode?)null
            };
            if (flag is not null)
                return (connection, flag.Value);
            if (!_interactive)
                throw new StepFailedException("La base ya tiene tablas. Indica qué hacer con --existing-db backup (respaldarlas y crear nuevas), " +
                                              "--existing-db reuse (usar las existentes y crear lo que falte) o --existing-db skip (no tocarla).");

            const string backup = "Respaldar las tablas existentes y crear nuevas (recomendado si hay datos)";
            const string reuse = "Usar las existentes y crear solo lo que falte";
            const string other = "Usar otra base de datos (otro nombre en el mismo servidor)";
            const string skip = "No tocar la base (solo dejar la migración lista)";
            var answer = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("La base ya tiene tablas. ¿Qué hago? [grey](nada se borra; antes de aplicar te muestro el detalle)[/]")
                .AddChoices(backup, reuse, other, skip));
            if (answer == backup) return (connection, ExistingDbMode.Backup);
            if (answer == reuse) return (connection, ExistingDbMode.Reuse);
            if (answer == skip) return (connection, ExistingDbMode.Skip);

            var newName = AnsiConsole.Prompt(new TextPrompt<string>("  Nombre de la otra base:"));
            connection = DatabaseSetup.WithDatabase(database, connection, newName);
            var check = await DatabaseSetup.TestAsync(database, connection);
            if (!check.Ok)
            {
                AnsiConsole.MarkupLine($"  [red]✘ No se pudo conectar:[/] {Markup.Escape(check.Message)}");
                return (null, ExistingDbMode.None);
            }
            AnsiConsole.MarkupLine($"  [green]✔[/] {Markup.Escape(check.Message)}");
        }
    }

    private static void ShowExistingTables(Dictionary<string, HashSet<string>> tables, Dictionary<string, long> rows)
    {
        var table = new Table().Border(TableBorder.Rounded).Title("[yellow]La base ya tiene tablas[/]")
            .AddColumn("Tabla").AddColumn(new TableColumn("Filas").RightAligned()).AddColumn("");
        foreach (var identity in SchemaInspector.IdentityTables)
        {
            var exists = tables.ContainsKey(identity);
            table.AddRow(identity, exists ? Rows(rows, identity) : "—", exists ? "[green]existe[/] (Identity)" : "[grey]falta (Identity)[/]");
        }
        var others = tables.Keys.Where(t => !SchemaInspector.IdentityTables.Contains(t, StringComparer.OrdinalIgnoreCase))
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var other in others.Take(15))
            table.AddRow(Markup.Escape(other), Rows(rows, other), "existe");
        if (others.Count > 15)
            table.AddRow($"… y {others.Count - 15} más", "", "");
        AnsiConsole.Write(table);
    }

    private static string Rows(Dictionary<string, long> rows, string table)
        => rows.TryGetValue(table, out var count) ? count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture) : "?";

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

    private sealed record RunResult(string? AdminPassword, IReadOnlyList<string> SecretsWrittenTo, bool MigrationApplied, bool Git,
        string? BackupSchema);

    private async Task<RunResult> RunAsync(ProjectChoices c, bool efOk)
    {
        string? admin = null;
        IReadOnlyList<string> written = [];
        var migrated = false;
        var git = false;
        string? backupSchema = null;

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
            }
        });

        // La base va fuera del spinner: si ya tenía tablas, hay que mostrar el plan y preguntar.
        if (efOk && c.ApplyMigrations && c.ConnectionString is not null)
            (migrated, backupSchema) = await PrepareDatabaseAsync(c);

        if (c.Git)
        {
            git = await LocalSetup.InitGitAsync(c);
            if (git) Done("Repositorio git inicializado");
        }
        return new RunResult(admin, written, migrated, git, backupSchema);
    }

    /// <summary>
    /// Crea las tablas según lo que se decidió para la base:
    /// vacía → migración normal; respaldo → mover las que chocan a un esquema de respaldo y migrar;
    /// reutilizar → copiar de respaldo las que se modifican, crear lo que falta y registrar la migración.
    /// Siempre se muestra el detalle y se pide confirmación antes de tocar una base con tablas.
    /// </summary>
    private async Task<(bool Migrated, string? BackupSchema)> PrepareDatabaseAsync(ProjectChoices c)
    {
        if (c.ExistingDb == ExistingDbMode.None)
        {
            await AnsiConsole.Status().StartAsync("Creando las tablas en la base…", async _ => await LocalSetup.ApplyMigrationsAsync(c));
            Done("Tablas creadas");
            return (true, null);
        }

        SchemaPlan plan = null!;
        await AnsiConsole.Status().StartAsync("Comparando la base existente con lo que necesita el proyecto…",
            async _ => plan = await SchemaInspector.BuildPlanAsync(c));
        var rows = await SchemaInspector.EstimateRowsAsync(c.Database, c.ConnectionString!);
        var schema = SchemaInspector.BackupSchemaName();

        if (c.ExistingDb == ExistingDbMode.Backup)
        {
            if (plan.ExistingTables.Count == 0)
            {
                AnsiConsole.MarkupLine("  [green]✔[/] Ninguna tabla del proyecto choca con las existentes: no hace falta respaldar.");
                await AnsiConsole.Status().StartAsync("Creando las tablas en la base…", async _ => await LocalSetup.ApplyMigrationsAsync(c));
                Done("Tablas creadas");
                return (true, null);
            }

            var moves = new Table().Border(TableBorder.Rounded)
                .Title($"[yellow]Respaldo: estas tablas se mueven intactas al esquema {schema}[/]")
                .AddColumn("Tabla").AddColumn(new TableColumn("Filas").RightAligned()).AddColumn("Después");
            foreach (var table in plan.ExistingTables)
                moves.AddRow(Markup.Escape(table), Rows(rows, table), "se crea una nueva, vacía");
            AnsiConsole.Write(moves);
            AnsiConsole.MarkupLine("  [grey]Se mueven con sus datos, índices y relaciones (no se copian ni se borran). " +
                                   "Si otras tablas tienen claves foráneas hacia ellas, seguirán apuntando a la copia respaldada.[/]");

            var move = SchemaInspector.MoveToBackupStatements(c.Database, schema, plan.ExistingTables);
            if (!ConfirmApply(move))
                return (false, null);

            await AnsiConsole.Status().StartAsync("Respaldando y creando las tablas…", async _ =>
            {
                await SchemaInspector.ApplyAsync(c.Database, c.ConnectionString!, move);
                await LocalSetup.ApplyMigrationsAsync(c);
            });
            Done($"{plan.ExistingTables.Count} tablas respaldadas en el esquema {schema} y tablas nuevas creadas");
            return (true, schema);
        }

        // Reutilizar: lo existente se conserva; se crea lo que falta.
        var detail = new Table().Border(TableBorder.Rounded).Title("[yellow]Plan sobre la base existente[/]")
            .AddColumn("Tabla").AddColumn(new TableColumn("Filas").RightAligned()).AddColumn("Acción");
        foreach (var table in plan.ExpectedTables)
        {
            var missingColumns = plan.MissingColumns.Where(m => m.Table.Equals(table, StringComparison.OrdinalIgnoreCase)).ToList();
            var action = plan.MissingTables.Contains(table) ? "[green]se crea[/]"
                : missingColumns.Count > 0 ? $"[yellow]se conserva y se agregan {missingColumns.Count} columnas:[/] {Markup.Escape(string.Join(", ", missingColumns.Select(m => m.Column)))}"
                : "se conserva tal cual";
            detail.AddRow(Markup.Escape(table), plan.MissingTables.Contains(table) ? "—" : Rows(rows, table), action);
        }
        AnsiConsole.Write(detail);

        var modified = plan.MissingColumns.Select(m => m.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var statements = new List<string>();
        if (modified.Count > 0)
        {
            AnsiConsole.MarkupLine($"  [grey]Antes de agregar columnas se guarda una copia completa de {string.Join(", ", modified)} en el esquema {schema}. " +
                                   "Las columnas nuevas que no admiten nulos se crean con un valor por defecto para las filas existentes.[/]");
            statements.AddRange(SchemaInspector.CopyToBackupStatements(c.Database, schema, modified));
        }
        statements.AddRange(plan.Statements);
        AnsiConsole.MarkupLine("  [grey]La migración InitialCreate queda registrada como aplicada: las próximas migraciones funcionan normal.[/]");

        if (!ConfirmApply(statements))
            return (false, null);

        await AnsiConsole.Status().StartAsync("Aplicando los cambios en una transacción…",
            async _ => await SchemaInspector.ApplyAsync(c.Database, c.ConnectionString!, statements));
        Done($"Base lista: {plan.MissingTables.Count} tablas creadas, {plan.MissingColumns.Count} columnas agregadas, nada existente borrado");
        return (true, modified.Count > 0 ? schema : null);
    }

    /// <summary>Confirma antes de tocar una base con tablas. Con --yes (y --existing-db) se aplica sin preguntar.</summary>
    private bool ConfirmApply(IReadOnlyList<string> statements)
    {
        if (!_interactive)
            return true;
        while (true)
        {
            var answer = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("¿Aplico estos cambios? [grey](todo en una transacción: si algo falla, no queda nada a medias)[/]")
                .AddChoices("Aplicar", "Ver el SQL", "No aplicar (dejar la migración lista)"));
            if (answer == "Aplicar")
                return true;
            if (answer.StartsWith("No", StringComparison.Ordinal))
                return false;
            AnsiConsole.Write(new Panel(Markup.Escape(string.Join("\n\n", statements))).Header("SQL").Expand());
        }
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
        if (r.BackupSchema is not null)
            lines.Add($"  [yellow]Respaldo:[/] las tablas anteriores quedaron intactas en el esquema {r.BackupSchema} de la misma base.");
        else if (c.ConnectionString is not null && !r.MigrationApplied)
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
