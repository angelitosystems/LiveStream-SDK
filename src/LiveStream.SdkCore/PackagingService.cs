using LiveStream.PluginSdk;

namespace LiveStream.SdkCore;

/// <summary>Todavía no hay paquete. Firmado lo muestra la CLI/GUI al leer <c>&lt;paquete&gt;.sig.json</c>.</summary>
public sealed record PackageInfo(string Path, PluginManifest Manifest, long SizeBytes, string Sha256, string? SignaturePath)
{
    public bool Signed => SignaturePath is not null && File.Exists(SignaturePath);
}

/// <summary>Empaquetado y comprobación de manifiestos, compartido por `lsplugin` y la GUI.</summary>
public static class PackagingService
{
    /// <summary>Valida un manifiesto de carpeta o un paquete completo (.lsplugin) y devuelve su manifiesto.</summary>
    public static PluginManifest Validate(string target)
    {
        var path = Path.GetFullPath(target);
        if (Directory.Exists(path))
        {
            var manifestPath = Path.Combine(path, PluginManifest.FileName);
            if (!File.Exists(manifestPath))
                throw new SdkStepException($"Falta {PluginManifest.FileName} en {path}.",
                    "Apunta a la carpeta de salida del plugin (bin/Release/net10.0/win-x64) o al proyecto ya publicado.");
            return PluginManifest.Parse(File.ReadAllText(manifestPath));
        }
        if (File.Exists(path))
            return PluginPackage.Inspect(path);
        throw new SdkStepException($"No existe el objetivo: {path}",
            "Ejemplos: la carpeta del ejecutable publicado o el archivo .lsplugin.");
    }

    /// <summary>Paquete (binario del plugin incluido) a .lsplugin. Devuelve ruta, tamaño y SHA-256.</summary>
    public static PackageInfo Pack(string directory, string? output = null)
    {
        var dir = Path.GetFullPath(directory);
        if (!Directory.Exists(dir))
            throw new SdkStepException($"No existe la carpeta: {dir}",
                "Ejemplo: bin/Release/net10.0/win-x64 creado por `lsplugin build`.");
        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(dir, PluginManifest.FileName)));
        if (!File.Exists(Path.Combine(dir, manifest.Entry)))
            throw new SdkStepException($"No existe «{manifest.Entry}» en {dir}.",
                "Compila primero: lsplugin build (o dotnet publish -c Release).");
        var destination = Path.GetFullPath(output ?? $"{manifest.Id}-{manifest.Version}{PluginPackage.Extension}");
        if (File.Exists(destination))
            File.Delete(destination);
        PluginPackage.Pack(dir, destination);
        var info = new FileInfo(destination);
        var signature = destination + PackageSignature.FileSuffix;
        return new PackageInfo(destination, manifest, info.Length, PluginPackage.Sha256(destination),
            File.Exists(signature) ? signature : null);
    }

    /// <summary>Instalación no permitida: el estado que debería cuidar una instalación se muestra antes de ejecutarla.</summary>
    public static IEnumerable<string> PermitSummary(PluginManifest manifest)
    {
        foreach (var permission in manifest.PermissionList)
            yield return $"• {permission} — {PluginPermissions.Descriptions.GetValueOrDefault(permission, permission)}";
        foreach (var (id, min) in manifest.Dependencies ?? new Dictionary<string, string>())
            yield return $"• depende del plugin «{id}» {min}";
    }
}
