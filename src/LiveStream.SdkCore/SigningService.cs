using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using LiveStream.PluginSdk;

namespace LiveStream.SdkCore;

/// <summary>Par de claves del publicador. La privada nunca sale del equipo ni debe subirse a ningún sitio.</summary>
public sealed record KeyPair(string PrivatePath, string PublicPath);

/// <summary>Generación de claves, firma y verificación del par de claves compatible con `PackageSignature`.</summary>
public static class SigningService
{
    public const string PrivateDefault = "private.pem";
    public const string PublicDefault = "plugin-public-key.pem";

    /// <summary>Ruta por omisión de la clave privada: %USERPROFILE%\.livestream-plugins\private.pem (fuera de git).</summary>
    public static string DefaultPrivatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".livestream-plugins", PrivateDefault);

    /// <summary>Crea el par de claves ECDSA P-256 con permisos del usuario y aviso si ya existe (--force pisa).</summary>
    public static KeyPair Keygen(string? privatePath = null, string? publicPath = null, bool force = false)
    {
        var privateFull = Path.GetFullPath(privatePath ?? DefaultPrivatePath);
        var publicFull = Path.GetFullPath(publicPath ?? PublicDefault);
        if ((File.Exists(privateFull) || File.Exists(publicFull)) && !force)
            throw new SdkStepException("Ya existe una clave en esas rutas.",
                "Usa --force solo si de verdad quieres sustituirla: los plugins firmados con la anterior dejarán de verificarse.");
        if (IsInsideGitRepository(privateFull))
            throw new SdkStepException("La clave privada no puede guardarse dentro de un repositorio git.",
                "Muévela a la carpeta por omisión: %USERPROFILE%\\.livestream-plugins (o a un disco aparte).");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Directory.CreateDirectory(Path.GetDirectoryName(privateFull)!);
        File.WriteAllText(privateFull, key.ExportPkcs8PrivateKeyPem());
        RestrictToCurrentUser(privateFull);
        Directory.CreateDirectory(Path.GetDirectoryName(publicFull)!);
        File.WriteAllText(publicFull, key.ExportSubjectPublicKeyInfoPem());
        return new KeyPair(privateFull, publicFull);
    }

    /// <summary>Escribe <c>&lt;paquete&gt;.sig.json</c> junto al paquete.</summary>
    public static PackageSignature Sign(string package, string privateKeyPath)
    {
        var keyText = ReadKey(privateKeyPath, "clave privada");
        PackageSignature signature;
        try
        {
            signature = PackageSignature.Create(package, keyText);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or PluginValidationException)
        {
            throw new SdkStepException($"No se pudo firmar el paquete: {ex.Message}",
                "Comprueba que el archivo de clave es un PEM del publicador (ECDSA P-256 de `lsplugin keygen`).");
        }
        File.WriteAllText(package + PackageSignature.FileSuffix, signature.ToJson());
        return signature;
    }

    /// <summary>Verifica que el paquete cargue, que la firma coincide y que la clave pública lo corrobora.</summary>
    public static PackageSignature Verify(string package, string publicKeyPath)
    {
        var signaturePath = package + PackageSignature.FileSuffix;
        if (!File.Exists(signaturePath))
            throw new SdkStepException($"El paquete no está firmado (falta {Path.GetFileName(signaturePath)}).",
                "Fírmalo con: lsplugin sign <paquete> --key <private.pem>.");
        var signature = PackageSignature.FromJson(File.ReadAllText(signaturePath));
        var publicKey = ReadKey(publicKeyPath, "clave pública");
        if (signature.VerifyPackage(package, publicKey) is { } error)
            throw new SdkStepException($"Firma no válida: {error}",
                "Asegúrate de usar la clave pública que corresponde al otro par de otra firma, y que el paquete no cambió tras firmarse.");
        return signature;
    }

    /// <summary>Una firma vecina del paquete (o null si no hay).</summary>
    public static PackageSignature? LoadSideward(string package)
    {
        var path = package + PackageSignature.FileSuffix;
        return File.Exists(path) ? PackageSignature.FromJson(File.ReadAllText(path)) : null;
    }

    private static string ReadKey(string path, string kind)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SdkStepException($"Falta la ruta de la {kind}.", "Pasa la ruta al archivo .pem con la opción correspondiente.");
        if (!File.Exists(path))
            throw new SdkStepException($"No existe la {kind}: {path}",
                "Genera el par con `lsplugin keygen` o apunta al archivo .pem correcto.");
        return File.ReadAllText(path);
    }

    public static bool IsInsideGitRepository(string path)
    {
        for (var dir = Path.GetDirectoryName(path); dir is not null; dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return true;
        return false;
    }

    /// <summary>Solo el usuario actual puede leer la clave privada del publicador.</summary>
    public static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return;
        }
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
