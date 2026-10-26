using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkStudio;

public partial class MainWindow : Window
{
    // ============ Plugins instalados (mismo PluginStore que la app) ============

    private async Task RefreshPluginsAsync()
    {
        var store = HostLocator.OpenStore();
        var plugins = store.List();
        _plugins.Clear();
        foreach (var plugin in plugins)
        {
            _plugins.Add(new PluginRow(plugin.Id, plugin.Manifest.Name, plugin.Record.Version,
                plugin.Record.Enabled, plugin.Record.Signed, plugin.Directory, plugin.Record.Source));
        }
        RefreshPluginsSummary(plugins);
    }

    private void RefreshPluginsSummary(IReadOnlyList<InstalledPlugin> plugins)
    {
        if (plugins.Count == 0)
        {
            PluginInfo.Text = "No hay plugins instalados en este equipo. Instala desde la CLI (`lsplugin install <paquete>`) o catálogo oficial de la app.";
            return;
        }
        var count = plugins.Count(p => p.Record.Enabled);
        PluginInfo.Text = $"{plugins.Count} plugin(s): {count} activo(s). La app lee el mismo estado (plugins.json), así que lo que desactives aquí lo respetará LiveStream.";
    }

    private void OnPluginsRefresh(object? sender, RoutedEventArgs args) => Post("Plugins", RefreshPluginsAsync);

    private async Task<InstalledPlugin?> AskPlugin(string heading)
    {
        if (PluginList.SelectedItem is not PluginRow row)
        {
            Report($"{heading}: elige un plugin de la lista.");
            return null;
        }
        var plugin = HostLocator.OpenStore().Find(row.Id);
        if (plugin is null)
        {
            Report($"{heading}: «{row.Id}» ya no está en el almacén; pulsa «Actualizar».");
            return null;
        }
        return plugin;
    }

    private void OnPluginsEnable(object? sender, RoutedEventArgs args) =>
        Post("Activar", async () =>
        {
            if (await AskPlugin("Activar") is not { } plugin)
                return;
            HostLocator.OpenStore().SetEnabled(plugin.Id, true);
            Report($"Activado {plugin.Id}.");
            await RefreshPluginsAsync();
        });

    private void OnPluginsDisable(object? sender, RoutedEventArgs args) =>
        Post("Desactivar", async () =>
        {
            if (await AskPlugin("Desactivar") is not { } plugin)
                return;
            // disable exige que nadie activo dependa de este (mismo contrato que la app).
            HostLocator.OpenStore().SetEnabled(plugin.Id, false);
            Report($"Desactivado {plugin.Id}.");
            await RefreshPluginsAsync();
        });

    private void OnPluginsUninstall(object? sender, RoutedEventArgs args) =>
        Post("Desinstalar", async () =>
        {
            if (await AskPlugin("Desinstalar") is not { } plugin)
                return;
            // Segunda lectura antes de tocar nada: el almacén se niega si otro plugin depende de este.
            var store = HostLocator.OpenStore();
            store.Uninstall(plugin.Id);
            Report($"Desinstalado {plugin.Id} (código y datos; el resto del estudio no se toca).");
            await RefreshPluginsAsync();
        });

    private void OnPluginsOpenFolder(object? sender, RoutedEventArgs args)
    {
        if (PluginList.SelectedItem is PluginRow row)
            OpenFolder(row.Directory);
        else
            Report("Plugins: elige uno de la lista para abrir su carpeta.");
    }

    // ============ Diagnóstico completo + informe copiable ============

    private void OnDoctorRefresh(object? sender, RoutedEventArgs args) =>
        Post("Diagnóstico", async () =>
        {
            var checks = await _doctor.CheckAsync();
            DoctorPanel.Children.Clear();
            var count = 0;
            foreach (var check in checks)
            {
                DoctorPanel.Children.Add(new TextBlock
                {
                    Text = (check.Ok ? "● " : "○ ") + check.Name,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = check.Ok ? Brush.Parse("#FF30A46C") : Brush.Parse("#FFF5A524"),
                });
                DoctorPanel.Children.Add(new TextBlock { Text = check.Detail, TextWrapping = TextWrapping.Wrap });
                if (check.Hint is { Length: > 0 } hint)
                    DoctorPanel.Children.Add(new TextBlock { Text = "→ " + hint, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#FF9AA1AB") });
                if (!check.Ok)
                    count++;
            }
            DoctorPanel.Children.Add(new TextBlock
            {
                Text = count == 0
                    ? "Comprobaciones: todo listo para crear, firmar e instalar plugins."
                    : $"{count} comprobación(es) pendiente(s).",
            });
            Subtitle.Text = $"Diagnóstico: {checks.Count(c => c.Ok)}/{checks.Count} correctas";
            Report(count == 0 ? "Diagnóstico: sin problemas." : $"Diagnóstico: {count} pendiente(s) — revisa el panel.");
        });

    private void OnDoctorCopy(object? sender, RoutedEventArgs args) =>
        Post("Copiar informe", async () =>
        {
            var checks = await _doctor.CheckAsync();
            var report = new StringBuilder()
                .AppendLine("LiveStream SDK — diagnóstico")
                .AppendLine($"Fecha: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · SDK {SdkLocations.SdkVersion()}")
                .AppendLine();
            foreach (var check in checks)
            {
                report.AppendLine($"{(check.Ok ? "[ok]" : "[!!]")} {check.Name} — {check.Detail}");
                if (check.Hint is { Length: > 0 } hint)
                    report.AppendLine($"     sugerencia: {hint}");
            }
            await (TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(report.ToString()) ?? Task.CompletedTask);
            Report("Informe copiado al portapapeles (sin claves ni rutas sensibles del publicador).");
        });
}
