using Spectre.Console;

namespace NetCoreApiKit;

/// <summary>Revisa lo que hace falta antes de preguntar nada: .NET SDK 10+, dotnet-ef y (si se usa) Docker.</summary>
public static class Prerequisites
{
    public static async Task<(bool Sdk, bool Ef)> CheckAsync()
    {
        var sdk = await Shell.RunAsync("dotnet", ["--version"]);
        var sdkOk = sdk.Ok && int.TryParse(sdk.Output.Trim().Split('.')[0], out var major) && major >= 10;
        var ef = (await Shell.RunAsync("dotnet", ["ef", "--version"])).Ok;

        AnsiConsole.MarkupLine($"  {Mark(sdkOk)} .NET SDK {Markup.Escape(sdk.Output.Trim())}   {Mark(ef)} dotnet-ef");
        if (!sdkOk)
            AnsiConsole.MarkupLine("  [red]Se necesita el .NET 10 SDK o superior:[/] https://dotnet.microsoft.com/download");
        return (sdkOk, ef);
    }

    public static async Task<bool> DockerRunningAsync() => (await Shell.RunAsync("docker", ["info", "--format", "{{.ServerVersion}}"])).Ok;

    public static async Task<bool> InstallEfAsync()
    {
        var result = await Shell.RunAsync("dotnet", ["tool", "install", "--global", "dotnet-ef"]);
        return result.Ok || result.Output.Contains("already installed", StringComparison.OrdinalIgnoreCase);
    }

    private static string Mark(bool ok) => ok ? "[green]✔[/]" : "[red]✘[/]";
}
