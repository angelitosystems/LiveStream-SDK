using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using LiveStream.PluginSdk;
using LiveStream.SdkCore;
using LiveStream.SdkTool;

namespace LiveStream.SdkTool;

/// <summary>Códigos de salida de `lsplugin`, documentados en docs/CLI.md.</summary>
internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Error = 1;
    public const int Usage = 2;
}

/// <summary>
/// Entrada de `lsplugin`. Este archivo es solo despachador, ayuda y control de errores: cada orden vive en
/// <see cref="CliCommands"/> / <see cref="PublishingCommands"/> y consume los servicios de LiveStream.SdkCore,
/// los mismos que usa la GUI. Códigos de salida: 0 correcto, 1 error de la operación, 2 uso incorrecto.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        var (ui, rest) = Args.ParseGlobal(args);
        try
        {
            return await RunAsync(ui, rest);
        }
        catch (UsageException ex)
        {
            ui.ErrorPlain(ex.Message, ex.Hint);
            return ExitCodes.Usage;
        }
        catch (SdkStepException ex)
        {
            ui.ErrorPlain(ex.Message, ex.Hint);
            return ExitCodes.Error;
        }
        catch (PluginValidationException ex)
        {
            ui.ErrorPlain(ex.Message, null);
            return ExitCodes.Error;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or UnauthorizedAccessException
            or CryptographicException or ArgumentException or InvalidOperationException or JsonException)
        {
            ui.ErrorPlain(ex.Message, null);
            return ExitCodes.Error;
        }
    }

    private static async Task<int> RunAsync(Ui ui, string[] rest)
    {
        if (rest.Length == 0)
        {
            Help.Section(ui);
            return ExitCodes.Ok;
        }
        if (rest[0] is "--help" or "-h" or "help" or "/?")
        {
            Help.Topic(ui, rest.Length > 1 ? rest[1] : null);
            return ExitCodes.Ok;
        }
        if (rest[0] is "--version" or "version")
        {
            ui.Line($"lsplugin {SdkLocations.SdkVersion()} (SDK de plugins de LiveStream)");
            ui.JsonOut(new { name = "lsplugin", sdk = SdkLocations.SdkVersion(), format = 1 });
            return ExitCodes.Ok;
        }

        var command = rest[0];
        var arguments = rest[1..];
        return command switch
        {
            "init" or "new" => await CliCommands.InitAsync(ui, arguments),
            "init-templates" => CliCommands.ListTemplates(ui),
            "completions" => CliCommands.Completions(ui, arguments),
            "doctor" => await CliCommands.DoctorAsync(ui, arguments),
            "validate" => await CliCommands.ValidateAsync(ui, arguments),
            "build" => await CliCommands.BuildAsync(ui, arguments),
            "pack" => await CliCommands.PackAsync(ui, arguments),
            "install" => await CliCommands.InstallAsync(ui, arguments),
            "list" => await CliCommands.ListAsync(ui, arguments),
            "info" => CliCommands.Info(ui, arguments),
            "enable" => CliCommands.Toggle(ui, arguments, enabled: true),
            "disable" => CliCommands.Toggle(ui, arguments, enabled: false),
            "uninstall" => CliCommands.Uninstall(ui, arguments),
            "config" => CliCommands.Config(ui, arguments),
            "keygen" => PublishingCommands.Keygen(ui, arguments),
            "sign" => PublishingCommands.Sign(ui, arguments),
            "verify" => PublishingCommands.Verify(ui, arguments),
            "publish" => await PublishingCommands.PublishAsync(ui, arguments),
            "yank" => await PublishingCommands.YankAsync(ui, arguments, unyank: false),
            "unyank" => await PublishingCommands.YankAsync(ui, arguments, unyank: true),
            "backup" => await PublishingCommands.BackupAsync(ui, arguments),
            _ => throw new UsageException($"Orden desconocida: «{command}».",
                $"Órdenes válidas: {Help.CommandList()} — detalles: lsplugin help <orden>."),
        };
    }
}

/// <summary>Ayuda contextual: pantalla general (por grupos) y ayuda por orden.</summary>
internal static class Help
{
    public static string CommandList() => string.Join(", ", new[]
    {
        "init", "validate", "build", "pack", "install", "list", "info", "enable", "disable", "uninstall",
        "doctor", "keygen", "sign", "verify", "publish", "yank", "unyank", "backup", "config",
        "completions", "version",
    });

    public static void Topic(Ui ui, string? topic)
    {
        if (topic is null or "-h" or "--help")
        {
            Section(ui);
            return;
        }
        var text = One(topic) ?? throw new UsageException($"No conozco la orden «{topic}».", CommandList());
        ui.Line($"\nlsplugin {topic}");
        ui.Line(text);
    }

    private static string? One(string topic) => topic switch
    {
        "init" or "new" => """
            Crea el proyecto completo de un plugin (manifiesto, csproj, Program.cs de la plantilla, .gitignore).

              lsplugin init <carpeta> --id com.empresa.plugin --name "Nombre"
              lsplugin init <carpeta> --template graphics --id com.empresa.graficos --name "Título"

            Plantillas: basic, graphics, sources, filters, integrations, tools (detalles: lsplugin init-templates).
            Sin --id/--name y en una terminal normal, los pregunta paso a paso; con --no-interactive los exige.
            """,
        "init-templates" => "Lista las plantillas de `init` (la GUI ofrece la misma lista).",
        "doctor" => """
            Diagnóstico del entorno: SDK .NET, app host, almacén de plugins, feed local, claves y marketplace.

              lsplugin doctor              # lista legible
              lsplugin doctor --json       # para scripts y la GUI
            """,
        "validate" => "Valida y muestra el manifiesto de una carpeta publicada o de un paquete .lsplugin.",
        "build" => """
            Compila el proyecto del plugin (dotnet publish -c Release) y devuelve la carpeta lista para `pack`.

              lsplugin build             # usa la carpeta y subcarpetas del proyecto actual
              lsplugin build .\MiPlugin
            """,
        "pack" => """
            Empaqueta una carpeta publicada (.exe + plugin.json) en un .lsplugin reproducible.

              lsplugin pack bin\Release\net10.0\win-x64\
              lsplugin pack bin\Release\net10.0\win-x64 -o salida.lsplugin
            """,
        "install" => """
            Instala un paquete en el almacén de LiveStream: valida firma/compatibilidad/dependencias, explica los
            permisos en claro y pide confirmación.

              lsplugin install .\MiPlugin.lsplugin --dev                        # prueba sin firma (modo desarrollador)
              lsplugin install .\paquete.lsplugin --public-key plugin-public.pem  # firmado vs esta clave

            La app abierta lo ve al reiniciarla; los datos del usuario del plugin se conservan al actualizar.
            """,
        "list" => "Plugins instalados en el equipo; `--server <url>` lista el catálogo del marketplace (propietario).",
        "info" => "Detalles de un plugin instalado (id, versión, permisos, ajustes, rutas, activación).",
        "enable" or "disable" => "Activa o desactiva el plugin respetando sus dependencias (enable exige tener activos los tuyos).",
        "uninstall" => "Quita código y datos del plugin; se niega si otro plugin depende de él. Pide confirmación.",
        "keygen" => """
            Crea el par de claves del publicador (ECDSA P-256).

              lsplugin keygen         # %USERPROFILE%\.livestream-plugins\private.pem
                                      # y plugin-public-key.pem en la carpeta actual
              lsplugin keygen --private ruta.pem --public otra.pem [--force]
            """,
        "sign" => "Firma un .lsplugin con la clave privada: escribe `<paquete>.sig.json` (no sube la clave).",
        "verify" => "Comprueba paquete y firma con la clave pública: lsplugin verify <paquete> --public <pem>.",
        "publish" => "Publica en el marketplace (solo el propietario, token en LS_MARKETPLACE_TOKEN): lsplugin publish <paquete> [--server https://…]",
        "yank" => "Retira una versión del catálogo del marketplace: lsplugin yank <id> <versión> [--server https://…]",
        "unyank" => "Restaura una versión retirada: lsplugin unyank <id> <versión> [--server https://…]",
        "backup" => "Descarga el respaldo de datos del marketplace: lsplugin backup -o copia.zip [--server https://…]",
        "config" => """
            Ajustes guardados (omisiones) en %APPDATA%\LiveStream\lsplugin.json:

              lsplugin config                          # mástralos
              lsplugin config --server https://…       # servidor por omisión del marketplace
            """,
        "completions" => "Escribe el autocompletado para tu terminal: pwsh, bash o zsh.",
        _ => null,
    };

    /// <summary>Pantalla de ayuda general, por grupos de órdenes.</summary>
    public static void Section(Ui ui) => ui.Line("""

        
          lsplugin — herramienta del SDK de plugins de LiveStream

            Crear y probar:    init  init-templates  validate  build  pack  install
            Gestionar:         list  info  enable  disable  uninstall    (probar en la app con doctor)
            Publicar:          keygen  sign  verify  publish  yank  unyank  backup
            Entorno:           doctor  config  completions  version  help

          Guía paso a paso:
            lsplugin init mi-plugin --id com.empresa.miplugin --name "Mi plugin"
            cd mi-plugin
            lsplugin build
            lsplugin pack bin\Release\net10.0\win-x64\
            lsplugin install .\com.empresa.miplugin-0.1.0.lsplugin --dev

          Banderas globales: --json | --quiet/-q | --verbose/-v | --no-interactive | --force
          Salida por códigos: 0 correcto · 1 error de operación · 2 uso incorrecto
          Detalle de cada orden:  lsplugin help <orden>        |  completo: docs\CLI.md
        """);
}
