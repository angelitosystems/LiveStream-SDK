using System.ComponentModel;
using System.Diagnostics;

namespace LiveStream.SdkCore;

/// <summary>Resultado de compilar un proyecto de plugin: la carpeta lista para `pack`.</summary>
public sealed record BuildResult(string ProjectPath, string TargetFramework, string OutputDirectory, string Log, int DurationMs)
{
    /// <summary>Ruta del ejecutable del plugin si el publish lo dejó en la carpeta de salida.</summary>
    public string? FindExecutable() =>
        Directory.EnumerateFiles(OutputDirectory, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
}

/// <summary>
/// `dotnet publish -c Release` como servicio compartido: la CLI (`lsplugin build`) y la GUI («Empaquetar») ejecutan
/// el mismo flujo y reciben la carpeta de salida lista para empaquetar.
/// </summary>
public static class BuildService
{
    public static async Task<BuildResult> PublishAsync(string directoryOrProject)
    {
        var start = Environment.TickCount64;
        var project = ResolveProject(Path.GetFullPath(directoryOrProject));
        var output = await RunPublish(project);
        var duration = (int)Math.Max(0, Environment.TickCount64 - start);
        if (!Directory.Exists(output))
            throw new SdkStepException($"`dotnet publish` no generó {output}.",
                "Revisa la salida completa del compilador con --verbose.");
        return new BuildResult(project, Path.GetFileName(output), output, output, duration);
    }

    private static string ResolveProject(string path)
    {
        if (File.Exists(path) && path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return path;
        if (Directory.Exists(path))
        {
            var projects = Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly);
            return projects.Length switch
            {
                1 => projects[0],
                0 => throw new SdkStepException($"En {path} no hay ningún proyecto .csproj.",
                    "Apunta a la carpeta del proyecto del plugin (la de plugin.json) o al .csproj."),
                _ => throw new SdkStepException($"En {path} hay {projects.Length} proyectos .csproj.",
                    "Apunta al proyecto concreto o a la carpeta que contiene exactamente uno."),
            };
        }
        throw new SdkStepException($"No existe el proyecto o carpeta: {path}",
            "Ejemplos: la carpeta creada por `lsplugin init` o el .csproj del plugin.");
    }

    private static async Task<string> RunPublish(string project)
    {
        string log;
        int exit;
        try
        {
            (log, exit) = await RunAsync("dotnet", $"publish \"{project}\" -c Release --nologo -v minimal");
        }
        catch (Win32Exception)
        {
            throw new SdkStepException("No se encontró la herramienta `dotnet` en el PATH.",
                "Instala el SDK de .NET 10 desde https://dotnet.microsoft.com/download y vuelve a intentarlo.");
        }
        if (exit != 0)
            throw new SdkStepException($"`dotnet publish` terminó con código {exit}.", Tail(log));
        return OutputDirectory(project);
    }

    private static string OutputDirectory(string project) =>
        Path.Combine(Path.GetDirectoryName(project)!, "bin", "Release", "net10.0", "win-x64");

    private static string Tail(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join("\n", lines.TakeLast(12));
    }

    private static async Task<(string Log, int ExitCode)> RunAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        var log = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) log.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return (log.ToString(), process.ExitCode);
    }
}
