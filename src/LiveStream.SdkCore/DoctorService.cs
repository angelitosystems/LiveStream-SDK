using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace LiveStream.SdkCore;

/// <summary>Una comprobación del diagnóstico con qué significa y qué hacer si falla.</summary>
public sealed record DoctorCheck(string Name, bool Ok, string Detail, string? Hint = null, string Section = "Entorno");

/// <summary>
/// Diagnóstico del entorno compartido por `lsplugin doctor` (JSON o tabla) y la página de Diagnóstico de la GUI:
/// los mismos checks, el mismo orden. No toca el estudio, no ejecuta código de plugins y no sale claves por consola.
/// </summary>
public sealed class DoctorService
{
    /// <summary>Ejecuta todas las comprobaciones; nunca lanza: cada check atrapa su propio fallo.</summary>
    public async Task<IReadOnlyList<DoctorCheck>> CheckAsync()
    {
        var checks = new List<DoctorCheck>
        {
            await CheckDotnetSdk(),
            CheckHost(),
            CheckPluginStore(),
            CheckSdkFeed(),
            CheckPublisherKey(),
            CheckMarketplaceConfig(),
        };
        return checks;
    }

    private static async Task<DoctorCheck> CheckDotnetSdk()
    {
        string? version = null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo("dotnet", "--list-sdks")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });
            if (process is null)
                return Fail("Herramienta dotnet", "No se pudo iniciar `dotnet`.", "Instala el SDK de .NET 10 desde https://dotnet.microsoft.com/download.");
            var output = await Task.Run(() => process.StandardOutput.ReadToEnd());
            await process.WaitForExitAsync();
            version = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split(' ')[0])
                .Where(v => v.StartsWith("10."))
                .FirstOrDefault();
        }
        catch (Win32Exception)
        {
            // Falta dotnet en el PATH: lo reporta el return de abajo.
        }
        return version is not null
            ? new DoctorCheck("SDK de .NET 10", true, $"Encontrada versión {version}")
            : Fail("SDK de .NET 10", "No se encontró el SDK de .NET 10 en el PATH.",
                "Instala el SDK de .NET 10 desde https://dotnet.microsoft.com/download (compilar el plugin lo requiere).");
    }

    private static DoctorCheck CheckHost()
    {
        // Localizador: la app host define los permisos que acepta cada plugin y su almacenamiento.
        return HostLocator.FindHost() is { } host
            ? new DoctorCheck("LiveStream instalado", true, $"{host.ExePath} (versión {host.Version ?? "desconocida"})")
            : Fail("LiveStream instalado", "No se encontró LiveStream.Desktop en el registro del usuario.",
                "Instala LiveStream desde su instalador oficial; la CLI prueba los plugins contra esa carpeta.");
    }

    private static DoctorCheck CheckPluginStore()
    {
        try
        {
            var store = HostLocator.OpenStore();
            var state = Directory.Exists(Path.GetDirectoryName(store.StatePath)!);
            var found = store.List();
            return new DoctorCheck("Almacén de plugins", true,
                $"{(state ? "Estado listo" : "Estado por crear")}: {store.StatePath}; {found.Count} plugin(s) instalado(s) en {store.InstallRoot}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException)
        {
            return Fail("Almacén de plugins", $"No se pudo leer el estado de plugins: {ex.Message}",
                "LiveStream lo recrea al abrir; ciérralo y revisa %APPDATA%\\LiveStream.");
        }
    }

    private static DoctorCheck CheckSdkFeed() =>
        SdkLocations.LocalFeed() is { } feed
            ? new DoctorCheck("Paquete NuGet local del SDK", true, $"Feed local: {feed}")
            : Fail("Paquete NuGet local del SDK", "No se encontró LIVESTREAM_SDK_DIR con carpeta packages.",
                "Ejecuta LiveStream-SDK-Setup-<versión>.exe de la última release para dejar el feed local; `lsplugin init` lo usa para restaurar sin conexión.");

    private static DoctorCheck CheckPublisherKey() =>
        File.Exists(SigningService.DefaultPrivatePath)
            ? new DoctorCheck("Clave del publicador", true, $"Existe en {SigningService.DefaultPrivatePath} (no se muestra el contenido).")
            : new DoctorCheck("Clave del publicador", false, "No existe %USERPROFILE%\\.livestream-plugins\\private.pem.",
                "Crea el par: lsplugin keygen. Sin ella puedes seguir compilando; solo firmas y publicas plugins.");

    private static DoctorCheck CheckMarketplaceConfig()
    {
        var config = SdkConfigStore.Load();
        var token = PublishingService.TokenFromEnvironment();
        return token is not null
            ? new DoctorCheck("Marketplace", true,
                $"Token cargado en {PublishingService.TokenVariable}; servidor por omisión: {config.Server ?? "(sin configurar, usa --server)"}.")
            : new DoctorCheck("Marketplace", false, $"No hay variable {PublishingService.TokenVariable}.",
                "El propietario publica con: set LS_MARKETPLACE_TOKEN=… (nunca en la línea de órdenes).");
    }

    private static DoctorCheck Fail(string name, string detail, string? hint = null) =>
        new(name, false, detail, hint);
}
