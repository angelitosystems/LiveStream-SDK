using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

// LiveStream-SDK-Setup: instala el SDK de plugins de LiveStream (el CLI «lsplugin») para tu usuario, lo añade al PATH
// del usuario, define LIVESTREAM_SDK_DIR y registra la desinstalación en «Aplicaciones instaladas» (HKCU). Nunca pide
// elevación. Uso:
//   LiveStream-SDK-Setup.exe [--quiet] [--dir <carpeta>]
//   LiveStream-SDK-Setup.exe --uninstall [--dir <carpeta>]
// Códigos de salida: 0 listo, 1 error.

const string EnvVar = "LIVESTREAM_SDK_DIR";
const string UninstallKeyName = "LiveStream SDK";
const string VersionFile = "sdk.version";
const string PayloadResource = "payload.zip";
const int Error = 1;

try
{
    if (args.Contains("--help") || args.Contains("-h"))
        return PrintHelp();
    return args.Contains("--uninstall") ? Uninstall(args) : Install(args);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return Error;
}

static int Install(string[] args)
{
    var dir = Path.GetFullPath(DirectoryArg(args) ?? DefaultDir());
    if (Path.GetPathRoot(dir) == dir)
        throw new ArgumentException("Elige una carpeta de destino, no la raíz de la unidad.");
    var quiet = args.Contains("--quiet");
    if (!quiet)
        Console.WriteLine($"LiveStream SDK {SdkVersion()}: instalando en {dir}…");

    ExtractPayload(dir);
    File.WriteAllText(Path.Combine(dir, VersionFile), $"{SdkVersion()}{Environment.NewLine}");
    CopySelf(dir);
    SetUserEnvironment(dir, keepVariable: true);
    WriteUninstallEntry(dir);
    BroadcastEnvironmentChange();

    if (!quiet)
        Console.WriteLine($"""
            Listo. «lsplugin» quedó en {dir}, añadido al PATH de tu usuario.

            Abre una terminal nueva y empieza:
              lsplugin new mi-plugin --id com.empresa.miplugin --name "Mi plugin"
              cd mi-plugin
              dotnet publish -c Release
              lsplugin pack bin\Release\net10.0\win-x64\

            Desinstalar: «Aplicaciones instaladas» → LiveStream SDK,
            o {Path.GetFileName(SetupPathIn(dir))} --uninstall
            """);
    return 0;
}

static int Uninstall(string[] args)
{
    string? registered = null;
    using (var key = Registry.CurrentUser.OpenSubKey(UninstallKeyName))
        registered = key?.GetValue("InstallLocation") as string;
    var dir = Path.GetFullPath(DirectoryArg(args) ?? registered ?? DefaultDir());
    Console.WriteLine($"LiveStream SDK {SdkVersion()}: desinstalando de {dir}…");
    if (Directory.Exists(dir))
    {
        // Solo borra la carpeta si el propio instalador la marcó: nunca una carpeta ajena ni una del repositorio.
        if (!File.Exists(Path.Combine(dir, VersionFile)))
            throw new ArgumentException($"{dir} no contiene {VersionFile}: el SDK solo se desinstala de la carpeta donde él se instaló. Con --dir apunta a otra carpeta si instalaste allí.");
        try
        {
            DeleteInstalled(dir);
        }
        catch (IOException)
        {
            Console.Error.WriteLine($"No se pudo borrar {dir}: cierra lsplugin y las terminales que lo usen y vuelve a intentarlo.");
            return Error;
        }
    }
    SetUserEnvironment(dir, keepVariable: false);
    Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyName, throwOnMissingSubKey: false);
    BroadcastEnvironmentChange();
    Console.WriteLine($"Desinstalado. Tus proyectos de plugins y las claves del publicador no se tocan; {EnvVar} se quita solo si apuntaba a {dir}.");
    return 0;
}

static void DeleteInstalled(string dir)
{
    var self = Environment.ProcessPath is { } path ? Path.GetFullPath(path) : null;
    var runningInside = self is not null && Path.GetDirectoryName(self)!.TrimEnd(Path.DirectorySeparatorChar)
        .Equals(dir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    if (!runningInside)
    {
        Directory.Delete(dir, recursive: true);
        return;
    }
    // La copia instalada no puede borrar el .exe en ejecución: borra el resto y deja un cmd desacoplado que
    // elimina el ejecutable y la carpeta apenas este proceso termina.
    foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
    {
        if (PathsEqual(entry, self!))
            continue;
        if (File.GetAttributes(entry).HasFlag(FileAttributes.Directory))
            Directory.Delete(entry, recursive: true);
        else
            File.Delete(entry);
    }
    _ = Process.Start(new ProcessStartInfo("cmd.exe",
        $"/c ping -n 2 127.0.0.1 > nul & del /f /q \"{self}\" & rmdir /s /q \"{dir}\"")
    { CreateNoWindow = true, UseShellExecute = false });
}

static void ExtractPayload(string dir)
{
    using var zip = new ZipArchive(EmbeddedPayload(), ZipArchiveMode.Read, leaveOpen: false);
    foreach (var entry in zip.Entries)
    {
        var target = Path.GetFullPath(Path.Combine(dir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"El paquete del SDK trae una ruta sospechosa: {entry.FullName}");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
            try
            {
                File.Delete(target);
            }
            catch (IOException)
            {
                throw new IOException($"«{Path.GetFileName(target)}» está en uso: cierra lsplugin y las terminales que lo abran, y vuelve a ejecutar el instalador.");
            }
        entry.ExtractToFile(target, overwrite: true);
    }
}

static Stream EmbeddedPayload() =>
    typeof(Program).Assembly.GetManifestResourceStream(PayloadResource)
        ?? throw new IOException("Este instalador se compiló sin el paquete del SDK: usa la versión oficial de GitHub Releases o genera la tuya con scripts/package.ps1.");

static void CopySelf(string dir)
{
    // Una copia del instalador dentro de la carpeta instalada es la que desinstala después; el .exe descargado no se queda.
    if (Environment.ProcessPath is not { } self || PathsEqual(self, SetupPathIn(dir)))
        return;
    try
    {
        File.Copy(self, SetupPathIn(dir), overwrite: true);
    }
    catch (IOException)
    {
        throw new IOException($"No se pudo copiar el instalador a {SetupPathIn(dir)}: cierra lsplugin y vuelve a ejecutar el instalador.");
    }
}

static void SetUserEnvironment(string dir, bool keepVariable)
{
    // HKEY_CURRENT_USER\Environment: PATH del usuario (sin expandir las %VAR% de terceros) y LIVESTREAM_SDK_DIR.
    using var env = Registry.CurrentUser.OpenSubKey("Environment", writable: true)
        ?? throw new InvalidOperationException("No se pudo abrir el registro del usuario.");
    var current = env.GetValue("Path", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    var updated = keepVariable ? SdkPath.Add(current, dir) : SdkPath.Remove(current, dir);
    if (updated is not null)
    {
        if (updated.Length == 0)
            env.DeleteValue("Path", throwOnMissingValue: false);
        else
            env.SetValue("Path", updated, RegistryValueKind.ExpandString);
    }
    if (keepVariable)
    {
        env.SetValue(EnvVar, dir, RegistryValueKind.String);
    }
    else if (env.GetValue(EnvVar, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is { } value
             && PathsEqual(value.ToString() ?? string.Empty, dir))
    {
        env.DeleteValue(EnvVar, throwOnMissingValue: false);
    }
}

static void WriteUninstallEntry(string dir)
{
    var sizeKb = (uint)(new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024);
    using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyName, writable: true);
    key.SetValue("DisplayName", "LiveStream SDK (lsplugin)");
    key.SetValue("DisplayVersion", SdkVersion());
    key.SetValue("Publisher", "Angelito Systems S.A.C.");
    key.SetValue("URLInfoAbout", "https://github.com/angelitosystems/LiveStream-SDK");
    key.SetValue("InstallLocation", dir);
    key.SetValue("DisplayIcon", Path.Combine(dir, "lsplugin.exe"));
    key.SetValue("UninstallString", $"\"{SetupPathIn(dir)}\" --uninstall");
    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
}

/// <summary>Carpeta por omisión del SDK, junto a la de los programas por usuario de LiveStream.</summary>
static string DefaultDir() =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LiveStream", "sdk");

static void BroadcastEnvironmentChange() =>
    _ = SendMessageTimeout((IntPtr)0xffff, 0x1A, IntPtr.Zero, "Environment", 2 /* SMTO_ABORTIFHUNG */, 5000, out _);

static string SdkVersion() =>
    (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0").Split('+')[0].Split('-')[0];

static string SetupPathIn(string dir) => Path.Combine(dir, "LiveStream-SDK-Setup.exe");

static bool PathsEqual(string a, string b) =>
    string.Equals(a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

static string? DirectoryArg(string[] args)
{
    var index = args.ToList().FindIndex(a => a is "--dir" or "/dir");
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int PrintHelp()
{
    Console.WriteLine("""
        LiveStream-SDK-Setup — instalador del SDK de plugins de LiveStream (por usuario, sin elevación)

          LiveStream-SDK-Setup.exe [--quiet] [--dir <carpeta>]      instala
          LiveStream-SDK-Setup.exe --uninstall [--dir <carpeta>]   desinstala

        Instala lsplugin.exe, el esquema (plugin.schema.json), la documentación y el paquete NuGet local del SDK;
        añade la carpeta al PATH del usuario y define LIVESTREAM_SDK_DIR. Desinstalar también desde
        «Aplicaciones instaladas» → LiveStream SDK.
        """);
    return 0;
}

[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

/// <summary>Operaciones puras sobre el contenido del PATH del usuario, sin registro ni consola, para poder probarlas.</summary>
public static class SdkPath
{
    /// <summary>Pone <paramref name="sdkDir"/> primero en el PATH (sin duplicar). Null si no habría cambios o el PATH no existe.</summary>
    public static string? Add(string? current, string sdkDir)
    {
        if (current is null)
            return null;
        var kept = Entries(current).Where(e => !Equal(e, sdkDir)).ToArray();
        var result = sdkDir + (kept.Length > 0 ? ';' + string.Join(';', kept) : string.Empty);
        return result == current ? null : result;
    }

    /// <summary>Quita <paramref name="sdkDir"/> del PATH. Null si no habría cambios; «» si queda vacío (se borra en el registro).</summary>
    public static string? Remove(string? current, string sdkDir)
    {
        if (current is null)
            return null;
        var result = string.Join(';', Entries(current).Where(e => !Equal(e, sdkDir)));
        return result == current ? null : result;
    }

    static string[] Entries(string current) =>
        current.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToArray();

    static bool Equal(string a, string b) =>
        string.Equals(a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
}
