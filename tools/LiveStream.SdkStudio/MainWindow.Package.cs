using Avalonia.Controls;
using Avalonia.Interactivity;

using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkStudio;

public partial class MainWindow : Window
{
    // ============ Manifiesto: editar plugin.json con validación inmediata ============

    private async void OnManifestChoose(object? sender, RoutedEventArgs args)
    {
        if (await ChooseFileAsync(package: false) is { } path)
        {
            ManifestPath.Text = path;
            LoadManifest(path);
        }
    }

    private void OnManifestLoad(object? sender, RoutedEventArgs args)
    {
        var input = Trim(ManifestPath);
        if (string.IsNullOrWhiteSpace(input))
        {
            Report("Manifiesto: elige un plugin.json con «Buscar…» o escribe su ruta.");
            return;
        }
        var path = ResolveManifestPath(input);
        if (path is null)
            return;
        ManifestPath.Text = path;
        LoadManifest(path);
    }

    private void LoadManifest(string path)
    {
        try
        {
            _manifestPath = path;
            ManifestText.Text = File.ReadAllText(path);
            ManifestStatus.Text = ValidateManifestText();
            Report($"Manifiesto abierto: {path}");
        }
        catch (Exception ex)
        {
            Report("Manifiesto: no se pudo abrir — " + ex.Message);
        }
    }

    private string ValidateManifestText()
    {
        try
        {
            if (PluginManifest.Parse(ManifestText.Text ?? "") is { } manifest)
                return $"✔ {manifest.Id} {manifest.Version} — válido (API {manifest.ApiVersion}, permisos: {string.Join(", ", manifest.PermissionList)})";
            return "El manifiesto está vacío.";
        }
        catch (PluginValidationException ex)
        {
            return "✘ " + ex.Message;
        }
    }

    private void OnManifestValidate(object? sender, RoutedEventArgs args) => ManifestStatus.Text = ValidateManifestText();

    private void OnManifestSave(object? sender, RoutedEventArgs args)
    {
        if (_manifestPath is null)
        {
            Report("Manifiesto: abre uno para poder guardarlo.");
            return;
        }
        try
        {
            // Se valida antes de escribir: si está roto, no se pierde el anterior.
            PluginManifest.Parse(ManifestText.Text ?? "");
            File.WriteAllText(_manifestPath, ManifestText.Text);
            ManifestStatus.Text = "Guardado y válido.";
            Report($"Guardado {_manifestPath}");
        }
        catch (Exception ex)
        {
            ManifestStatus.Text = "✘ " + ex.Message;
            Report("Manifiesto: no se guardó (el archivo anterior está intacto) — " + ex.Message);
        }
    }

    /// <summary>Acepta la carpeta del proyecto o la ruta del JSON mismo.</summary>
    private static string? ResolveManifestPath(string input)
    {
        var candidate = Path.GetFullPath(input);
        if (File.Exists(candidate))
            return PluginManifest.FileName.Equals(Path.GetFileName(candidate), StringComparison.OrdinalIgnoreCase)
                ? candidate
                : Path.Combine(candidate, PluginManifest.FileName);
        if (Directory.Exists(candidate))
        {
            var manifest = Path.Combine(candidate, PluginManifest.FileName);
            if (File.Exists(manifest))
                return manifest;
            var published = Path.Combine(candidate, "bin", "Release", "net10.0", "win-x64", PluginManifest.FileName);
            if (File.Exists(published))
                return published;
            return candidate; // deja que el Save/Load explique el error concreto por su cuenta.
        }
        return null;
    }

    // ============ Paquete: validar / compilar / empaquetar / firmar / verificar ============

    private async void OnPackageChoose(object? sender, RoutedEventArgs args)
    {
        // Primero intenta un .lsplugin; si no pides una carpeta publicada (validar/compilar/empaquetar funcionan igual).
        if (await ChooseFileAsync(package: true) is { } file && file.EndsWith(".lsplugin", StringComparison.OrdinalIgnoreCase))
        {
            PackageInput.Text = file;
            return;
        }
        if (await ChooseFolderAsync() is { } folder)
            PackageInput.Text = folder;
    }

    private void OnPackageValidate(object? sender, RoutedEventArgs args) =>
        Post("Validar", async () =>
        {
            var manifest = PackagingService.Validate(Trim(PackageInput));
            PackageSummary.Text = $"{manifest.Id} {manifest.Version} — API {manifest.ApiVersion} · entry {manifest.Entry} · permisos {string.Join(", ", manifest.PermissionList)}";
            Report($"Válido: {manifest.Id} {manifest.Version}");
        });

    private void OnPackageBuild(object? sender, RoutedEventArgs args) =>
        Post("Compilar", async () =>
        {
            Report("Compilando (dotnet publish -c Release)…");
            var build = await BuildService.PublishAsync(Trim(PackageInput));
            PackageInput.Text = build.OutputDirectory;
            Report($"Compilación lista en {build.OutputDirectory} ({build.DurationMs} ms).");
        });

    private void OnPackagePack(object? sender, RoutedEventArgs args) =>
        Post("Empaquetar", async () =>
        {
            var info = PackagingService.Pack(Trim(PackageInput));
            PackageSummary.Text = $"Paquete: {info.Path} ({info.SizeBytes / 1024} KB) · sha256 {info.Sha256}";
            Report(info.Signed
                ? $"Empaquetado {info.Path} (ya tenía firma {info.SignaturePath})."
                : $"Empaquetado {info.Path}. Sin firma: LiveStream solo lo instalará en Modo desarrollador; fírmalo antes de repartirlo.");
        });

    private void OnPackageSign(object? sender, RoutedEventArgs args) =>
        Post("Firmar", async () =>
        {
            var package = RequirePackage();
            var signature = SigningService.Sign(package, SigningService.DefaultPrivatePath);
            PackageSummary.Text = $"Firmado {package}{PackageSignature.FileSuffix} — {signature.Id} {signature.Version} (sha256 {signature.Sha256})";
            Report($"Firmado {package}; la clave privada no sale de este equipo.");
        });

    private void OnPackageVerify(object? sender, RoutedEventArgs args) =>
        Post("Verificar", async () =>
        {
            var package = RequirePackage();
            var publicKey = Path.Combine(Path.GetDirectoryName(package)!, "plugin-public-key.pem");
            if (!File.Exists(publicKey))
                publicKey = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".livestream-plugins", "plugin-public-key.pem");
            var signature = SigningService.Verify(package, publicKey);
            PackageSummary.Text = $"Firma válida: {signature.Id} {signature.Version} (paquete {signature.Sha256})";
            Report($"Firma verificada contra {publicKey}.");
        });

    private string RequirePackage()
    {
        var package = Trim(PackageInput);
        if (!File.Exists(package))
            throw new SdkStepException($"No existe el paquete: {package}",
                "Apunta al archivo .lsplugin (o a la carpeta publicada y luego «Empaquetar»).");
        return package;
    }
}
