using System.Security;
using Microsoft.Win32;

namespace LiveStream.SdkCore;

/// <summary>Detecta la instalación de LiveStream (la app host) en el registro por usuario y comprueba su marcador.</summary>
public static class HostLocator
{
    /// <summary>Estructura y versiones dadas por el instalador de la app (LiveStream.Desktop instalado por usuario).</summary>
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LiveStream";
    /// <summary>Mismo marcador que deja LiveStream-Setup: distinguir una carpeta instalada de otra cualquiera.</summary>
    public const string MarkerFile = "livestream.install";
    public const string ExecutableName = "LiveStream.Desktop.exe";

    public sealed record HostSnapshot(string InstallLocation, string ExePath, string? Version);

    public static HostSnapshot? FindHost()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            var location = key?.GetValue("InstallLocation") as string;
            if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location))
                return null;
            var exe = Path.Combine(location, ExecutableName);
            if (!File.Exists(exe) || !File.Exists(Path.Combine(location, MarkerFile)))
                return null;
            var version = key?.GetValue("DisplayVersion") as string;
            return new HostSnapshot(location, exe, version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>Almacén de plugins tal como lo usa la app (`%LOCALAPPDATA%\LiveStream\plugins` + estado en %APPDATA%).</summary>
    public static PluginStore OpenStore()
    {
        var appVersion = FindHost()?.Version is { } text ? Version.Parse(text) : new Version(0, 0, 0);
        return new PluginStore(appVersion, trustedPublicKey: null);
    }
}
