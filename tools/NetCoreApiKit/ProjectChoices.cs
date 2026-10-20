namespace NetCoreApiKit;

public enum DbLocation { Docker, Local, Server, Later }

public enum SecretStore { UserSecrets, Environment, AppSettingsLocal }

/// <summary>Qué hacer si la base elegida ya tiene tablas.</summary>
public enum ExistingDbMode
{
    None,      // la base está vacía o no existe
    Backup,    // mover las tablas que chocan a un esquema de respaldo y crear nuevas
    Reuse,     // usar las existentes y crear solo lo que falte (copia de respaldo de las que se modifican)
    Skip       // no tocar la base
}

/// <summary>Todas las respuestas del asistente: lo que se pidió por flags o por preguntas.</summary>
public sealed class ProjectChoices
{
    public required string Name { get; init; }
    public required string OutputDirectory { get; init; }
    public bool Security { get; init; }
    public bool ApiKey { get; init; }
    public required string Database { get; init; }          // postgresql | sqlserver
    public required string Email { get; init; }             // smtp | log
    public bool Analytics { get; init; }
    public DbLocation DbLocation { get; init; }
    public string? ConnectionString { get; set; }            // local/server (docker se arma después de levantar el contenedor)
    public bool ApplyMigrations { get; init; }
    public ExistingDbMode ExistingDb { get; init; }
    public required IReadOnlySet<SecretStore> SecretStores { get; init; }
    public bool PersistEnvironment { get; init; }
    public bool Git { get; init; }
    public string? TemplateHive { get; init; }

    public string LowerName => Name.ToLowerInvariant();
    public bool PostgreSql => Database == "postgresql";
    public bool Smtp => Security && Email == "smtp";
    public string ApiProjectDirectory => Path.Combine(OutputDirectory, "src", $"{Name}.Api");
    public string SolutionFile => $"{Name}.slnx";
}
