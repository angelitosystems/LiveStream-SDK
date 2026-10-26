using System.Diagnostics;

namespace LiveStream.Cli.Tests;

/// <summary>
/// Localiza y ejecuta el binario real de `lsplugin` construido por la referencia de proyecto
/// (tools\LiveStream.PluginTool\bin\&lt;config&gt;\net10.0\lsplugin.exe). Los comandos elegidos para e2e
/// (help, version, init, validate, doctor, orden desconocida) no requieren `dotnet publish` ni red.
/// </summary>
internal static class CliRunner
{
    public static readonly string Exe = FindExe()
        ?? throw new InvalidOperationException("No se encontró lsplugin.exe construido; compila la solución primero.");

    /// <summary>Carpeta temporal aislada para una prueba (el llamador la borra).</summary>
    public static string TempFolder(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lscli-tests", Guid.NewGuid().ToString("N")[..8], name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Ejecuta el proceso real y devuelve su salida combinada y código de salida (timeout 120 s).</summary>
    public static (int Code, string Output) Run(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // El CLI escribe UTF-8 (new UTF8Encoding(false) en Main); el padre debe decodificarlo igual.
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(120_000))
            throw new TimeoutException($"lsplugin {string.Join(' ', arguments)} no terminó en 120 s.");
        return (process.ExitCode, output.ToString());
    }

    /// <summary>Extrae el primer bloque JSON completo del stdout (las órdenes imprimen texto y JSON).</summary>
    public static T? ParseJson<T>(string output)
        where T : class
    {
        static System.Text.Json.JsonDocument? TryParse(string candidate)
        {
            try { return System.Text.Json.JsonDocument.Parse(candidate); }
            catch (System.Text.Json.JsonException) { return null; }
        }
        var document = TryParse(output.Trim())
            ?? TryParse(output[output.IndexOf('[')..].Trim())
            ?? TryParse(output[output.IndexOf('{')..].Trim());
        if (document is null)
            return null;
        using (document)
            return System.Text.Json.JsonSerializer.Deserialize<T>(document.RootElement,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                });
    }

    /// <summary>tools\LiveStream.PluginTool\bin existe en Release o Debug (la referencia de proyecto ya la construyó).</summary>
    private static string? FindExe()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (!Directory.Exists(Path.Combine(dir.FullName, "tools", "LiveStream.PluginTool")))
                continue;
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(dir.FullName, "tools", "LiveStream.PluginTool", "bin", configuration,
                    "net10.0", "lsplugin.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
