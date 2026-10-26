using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkTool;

/// <summary>Órdenes del almacén local de `lsplugin`: instalar, listar, informar, activar/desactivar, desinstalar y config.</summary>
public static partial class CliCommands
{
    /// <summary>Abre el almacén según el modo de instalación pedido: --dev (sin firma), --public-key o normal.</summary>
    private static PluginStore OpenStore(string[] rest, out string way)
    {
        var host = HostLocator.FindHost();
        var store = new PluginStore(
            Version.TryParse(host?.Version, out var detected) ? detected : new Version(0, 0, 0),
            trustedPublicKey: null);
        if (Args.Has(rest, "--dev"))
        {
            way = "modo desarrollador (sin verificar firma)";
            store.DeveloperMode = true;
            return store;
        }
        if (Args.Option(rest, "--public-key") is { } keyPath)
        {
            if (!File.Exists(keyPath))
                throw new SdkStepException($"No existe la clave pública: {keyPath}",
                    "Apunta al .pem del publicador (el que firmó este paquete).");
            way = $"verificado con la clave {Path.GetFileName(keyPath)}";
            return new PluginStore(store.AppVersion, File.ReadAllText(keyPath), store.InstallRoot, store.DataRoot);
        }
        way = host is not null ? $"host LiveStream {host.Version}" : "almacén por usuario (sin app detectada)";
        return store;
    }

    public static async Task<int> InstallAsync(Ui ui, string[] rest)
    {
        var package = Path.GetFullPath(Args.Positional(rest, 0,
            "Ejemplo: lsplugin install .\\com.empresa.plugin-0.1.0.lsplugin [--dev | --public-key pub.pem] [--yes]"));
        if (!File.Exists(package))
            throw new SdkStepException($"No existe el paquete: {package}");
        var signature = SigningService.LoadSideward(package);
        var store = OpenStore(rest, out var way);
        var manifest = store.Verify(package, signature);
        ui.JsonOut(new
        {
            package,
            signaturePresent = signature is not null,
            way,
            id = manifest.Id,
            name = manifest.Name,
            version = manifest.Version,
            minAppVersion = manifest.MinAppVersion,
            permissions = manifest.PermissionList
                .Select(p => new { id = p, description = PluginPermissions.Descriptions.GetValueOrDefault(p, p) })
                .ToArray(),
            dependencies = manifest.Dependencies ?? new Dictionary<string, string>(),
            replaces = store.Find(manifest.Id) is { } previous ? previous.Manifest.Version : null,
        });
        ui.Ok($"Instalación de {manifest.Id} {manifest.Version} — {way}");
        ui.Detail("Validado: firma (con la clave indicada), compatibilidad con la app y dependencias instaladas.");
        ui.Line("Permisos que el usuario aceptó al firmarlo (los mismos que mostraría LiveStream):");
        foreach (var line in PackagingService.PermitSummary(manifest))
            ui.Line("  " + line);
        if (store.Find(manifest.Id) is { } installedBefore && installedBefore.Manifest.Version != manifest.Version)
            ui.Warn($"Es una actualización desde {installedBefore.Manifest.Version}: los datos del plugin se conservan.");
        if (!Args.Has(rest, "--yes", "-y") && !ui.Confirm("¿Instalarlo ahora?", assumeDefault: false))
        {
            ui.Line("No instalado.");
            return ExitCodes.Error;
        }
        ui.Line("Instalando…");
        var installed = store.Install(package, signature, source: package, enable: true);
        ui.Ok($"Instalado: {installed.Manifest.Id} {installed.Manifest.Version} → {installed.Directory}");
        ui.Warn("La app lo ve al reiniciarla (Herramientas → Marketplace de plugins).");
        return ExitCodes.Ok;
    }

    public static async Task<int> ListAsync(Ui ui, string[] rest)
    {
        if (Args.Option(rest, "--server") is { } server)
        {
            var items = await new PublishingService(server, PublishingService.TokenFromEnvironment()).ListAsync();
            ui.JsonOut(items);
            ui.Line("Catálogo del marketplace (propietario):");
            foreach (var item in items)
                ui.Line($"  {item.Id,-40} {item.Version,-10} {(item.Yanked ? "RETIRADA" : "publicada")} {item.PublishedAt}");
            if (items.Count == 0)
                ui.Line("  (sin versiones)");
            return ExitCodes.Ok;
        }
        var store = HostLocator.OpenStore();
        var plugins = store.List();
        ui.JsonOut(plugins.Select(p => new
        {
            id = p.Id,
            name = p.Manifest.Name,
            version = p.Record.Version,
            enabled = p.Record.Enabled,
            signed = p.Record.Signed,
            installedAt = p.Record.InstalledAt,
            directory = p.Directory,
        }));
        ui.Line($"Plugins instalados en {store.InstallRoot}:");
        if (plugins.Count == 0)
        {
            ui.Line("  (ninguno; instala uno con: lsplugin install <paquete>)");
            return ExitCodes.Ok;
        }
        foreach (var plugin in plugins)
            ui.Line($"  {plugin.Id,-40} {plugin.Record.Version,-8} {(plugin.Record.Enabled ? "activo" : "inactivo")} {(plugin.Record.Signed ? "firmado" : "sin firma")}");
        return ExitCodes.Ok;
    }

    public static int Info(Ui ui, string[] rest)
    {
        var id = Args.Positional(rest, 0, "Id del plugin instalado: lsplugin info com.empresa.plugin [--json]");
        var store = HostLocator.OpenStore();
        var plugin = store.Find(id) ?? throw new SdkStepException($"«{id}» no está instalado.",
            "Lista lo que hay con: lsplugin list");
        var record = plugin.Record;
        var settings = ReadSettings(store, plugin);
        ui.JsonOut(new
        {
            id = plugin.Id,
            name = plugin.Manifest.Name,
            version = record.Version,
            enabled = record.Enabled,
            signed = record.Signed,
            source = record.Source,
            installedAt = record.InstalledAt,
            entry = plugin.EntryPath,
            manifest = new
            {
                plugin.Manifest.Author,
                plugin.Manifest.Description,
                plugin.Manifest.Category,
                plugin.Manifest.MinAppVersion,
                plugin.Manifest.ApiVersion,
                permissions = plugin.Manifest.PermissionList,
                dependencies = plugin.Manifest.Dependencies,
                settings,
            },
            directory = plugin.Directory,
        });
        ui.Line($"Id:         {plugin.Id}");
        ui.Line($"Nombre:     {plugin.Manifest.Name}");
        ui.Line($"Versión:    {record.Version}  ({(record.Enabled ? "activo" : "inactivo")}, {record.Source}, {record.InstalledAt:yyyy-MM-dd HH:mm})");
        ui.Line($"Dirección:  {plugin.Directory}");
        ui.Line($"Ejecutable: {plugin.EntryPath}");
        ui.Line($"Permisos:   {string.Join(", ", plugin.Manifest.PermissionList)}");
        if (settings.Count > 0)
            ui.Line($"Ajustes:    {string.Join(", ", settings.Select(s => $"{s.Key}={s.Value}"))}");
        return ExitCodes.Ok;
    }

    /// <summary>Ajustes guardados por la app con sus valores actuales (o los declarados por el manifiesto).</summary>
    private static Dictionary<string, string> ReadSettings(PluginStore store, InstalledPlugin plugin)
    {
        try
        {
            return store.LoadSettings(plugin).ToDictionary(s => s.Key, s => s.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PluginValidationException)
        {
            return (plugin.Manifest.Settings ?? []).ToDictionary(s => s.Key, s => s.Default);
        }
    }

    public static int Toggle(Ui ui, string[] rest, bool enabled)
    {
        var id = Args.Positional(rest, 0, $"Ejemplo: lsplugin {(enabled ? "enable" : "disable")} com.empresa.plugin");
        var store = HostLocator.OpenStore();
        store.SetEnabled(id, enabled);
        ui.JsonOut(new
        {
            id,
            enabled = enabled,
        });
        ui.Ok($"{(enabled ? "Activado" : "Desactivado")}: {id}");
        return ExitCodes.Ok;
    }

    public static int Uninstall(Ui ui, string[] rest)
    {
        var id = Args.Positional(rest, 0, "Id del plugin instalado (lsplugin list): lsplugin uninstall com.empresa.plugin [--yes]");
        var store = HostLocator.OpenStore();
        var plugin = store.Find(id) ?? throw new SdkStepException($"«{id}» no está instalado.",
            "Almacén vacío o id distinto; mira la lista con: lsplugin list");
        ui.JsonOut(new
        {
            id = plugin.Id,
            name = plugin.Manifest.Name,
            version = plugin.Record.Version,
            directory = plugin.Directory,
            dataDirectory = store.DataDirectory(plugin.Id),
        });
        ui.Line($"Se quitarán {plugin.Manifest.Name} {plugin.Record.Version} y sus datos de plugin (config del usuario).");
        if (!Args.Has(rest, "--yes", "-y") && !ui.Confirm("Vamos a desinstalarlo", assumeDefault: false))
        {
            ui.Line("No desinstalado.");
            return ExitCodes.Error;
        }
        store.Uninstall(id);
        ui.Ok($"Desinstalado: {id}");
        return ExitCodes.Ok;
    }

    public static int Config(Ui ui, string[] rest)
    {
        var config = SdkConfigStore.Load();
        if (Args.Option(rest, "--server") is { } server)
        {
            config = config.WithServer(server);
            SdkConfigStore.Save(config);
            ui.Ok($"Servidor por omisión del marketplace: {server}");
        }
        ui.JsonOut(new { schema = config.Schema, server = config.Server, location = SdkConfigStore.Location });
        ui.Line($"Configuración guardada en {SdkConfigStore.Location}");
        ui.Line($"  servidor del marketplace por omisión: {config.Server ?? "(no configurado; usa --server)"}");
        return ExitCodes.Ok;
    }
}
