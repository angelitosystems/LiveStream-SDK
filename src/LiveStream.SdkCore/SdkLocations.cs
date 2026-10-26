using System.Reflection;

namespace LiveStream.SdkCore;

/// <summary>
/// Localiza piezas de la instalación/entorno: el feed local del paquete NuGet (SDK instalado), el proyecto fuente
/// (repositorio) y la versión del paquete del SDK para las referencias del scaffold.
/// </summary>
public static class SdkLocations
{
    /// <summary>Carpeta del feed local que `LiveStream-SDK-Setup` dejó junto a lsplugin (variable LIVESTREAM_SDK_DIR).</summary>
    public static string? LocalFeed()
    {
        var installed = Environment.GetEnvironmentVariable("LIVESTREAM_SDK_DIR");
        if (!string.IsNullOrWhiteSpace(installed) && Directory.Exists(Path.Combine(installed, "packages")))
            return Path.Combine(installed, "packages");
        var local = Path.Combine(AppContext.BaseDirectory, "packages");
        return Directory.Exists(local) ? local : null;
    }

    /// <summary>Proyecto del SDK de plugins si esta herramienta se ejecuta dentro de su repositorio.</summary>
    public static string? RepoSdkPath()
    {
        // AppContext.BaseDirectory para la carpeta del ejecutable (también en publicación single-file, donde
        // Assembly.Location vuelve vacío).
        var homes = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
        return homes.SelectMany(home =>
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(home ?? "."));
                return dir is null ? [] : WalkUp(dir);
            })
            .Select(dir => Path.Combine(dir, "src", "LiveStream.PluginSdk", "LiveStream.PluginSdk.csproj"))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Versión del paquete LiveStream.PluginSdk que acompañó a esta compilación (MAYOR.MENOR.PARCHE).</summary>
    public static string SdkVersion()
    {
        var informational = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = (informational ?? "1.0.0").Split('+')[0];
        var parts = version.Split('.');
        return string.Join('.', parts.Take(3).Concat(parts.Length == 2 ? ["0"] : []));
    }

    private static IEnumerable<string> WalkUp(string directory)
    {
        for (var dir = directory; dir is not null; dir = Path.GetDirectoryName(dir))
            yield return dir;
    }
}
