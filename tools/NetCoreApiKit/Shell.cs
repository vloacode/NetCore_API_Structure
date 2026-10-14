using System.Diagnostics;
using System.Security.Cryptography;

namespace NetCoreApiKit;

/// <summary>Resultado de un comando externo.</summary>
public sealed record ShellResult(int ExitCode, string Output)
{
    public bool Ok => ExitCode == 0;

    /// <summary>Últimas líneas de la salida, para mostrar un error sin inundar la consola.</summary>
    public string Tail(int lines = 25) => string.Join(Environment.NewLine, Output.Split('\n').TakeLast(lines)).Trim();
}

public static class Shell
{
    /// <summary>Ejecuta un comando y captura stdout+stderr. Los secretos se pasan por <paramref name="env"/>, nunca por argumentos.</summary>
    public static async Task<ShellResult> RunAsync(string file, IEnumerable<string> args, string? workingDirectory = null,
        IDictionary<string, string>? env = null)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_NOLOGO"] = "1";
        if (env is not null)
            foreach (var (key, value) in env)
                info.Environment[key] = value;

        try
        {
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ShellResult(process.ExitCode, await stdout + await stderr);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ShellResult(-1, $"No se encontró '{file}': {ex.Message}");
        }
    }

    public static Task<ShellResult> DotnetAsync(string workingDirectory, params string[] args) => RunAsync("dotnet", args, workingDirectory);
}

/// <summary>Valores aleatorios para secretos de desarrollo.</summary>
public static class Secrets
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "#%*+-=@_";

    /// <summary>Llave larga (JWT, HMAC de analítica): 64 bytes en Base64 URL.</summary>
    public static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Contraseña que cumple la política de Identity y de SQL Server (mayúscula, minúscula, dígito, símbolo).</summary>
    public static string Password(int length = 20)
    {
        var all = Lower + Upper + Digits + Symbols;
        var chars = new List<char>
        {
            Lower[RandomNumberGenerator.GetInt32(Lower.Length)],
            Upper[RandomNumberGenerator.GetInt32(Upper.Length)],
            Digits[RandomNumberGenerator.GetInt32(Digits.Length)],
            Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]
        };
        while (chars.Count < length)
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    /// <summary>Contraseña sin símbolos especiales de shell/URL, para la base en Docker (va en .env y en la cadena de conexión).</summary>
    public static string DatabasePassword()
    {
        var value = Password(24);
        return "Db" + new string(value.Where(char.IsLetterOrDigit).ToArray()) + "9x";
    }
}
