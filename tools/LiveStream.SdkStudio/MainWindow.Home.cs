using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkStudio;

public partial class MainWindow : Window
{
    // ============ Inicio: estado del entorno y accesos rápidos ============

    private async Task RefreshHomeAsync()
    {
        var checks = await _doctor.CheckAsync();
        _homeChecks.Clear();
        foreach (var check in checks)
            _homeChecks.Add(new CheckRow(check.Ok, check.Name, check.Detail, check.Hint));
        Subtitle.Text = $"SDK {SdkLocations.SdkVersion()} · {checks.Count(c => c.Ok)}/{checks.Count} comprobaciones correctas";
        if (HostLocator.FindHost() is { } host)
            HomeHost.Text = $"LiveStream {host.Version ?? "?"} — {host.ExePath}";
        else
            HomeHost.Text = "LiveStream no está instalado en este equipo (prueba los plugins dentro de la app cuando la tengas).";
        HomeRecents.Text = "Crea tu primer proyecto en la pestaña «Crear plugin»; después encontrarás carpetas y datos en los accesos rápidos.";
    }

    private void OnHomeRefresh(object? sender, RoutedEventArgs args) => Post("Inicio", RefreshHomeAsync);

    private void OnHomeOpenPlugins(object? sender, RoutedEventArgs args)
    {
        var store = HostLocator.OpenStore();
        Directory.CreateDirectory(store.InstallRoot);
        OpenFolder(store.InstallRoot);
    }

    private void OnHomeOpenData(object? sender, RoutedEventArgs args)
    {
        var store = HostLocator.OpenStore();
        Directory.CreateDirectory(store.DataRoot);
        OpenFolder(store.DataRoot);
    }

    private void OnHomeNew(object? sender, RoutedEventArgs args)
    {
        Tabs.SelectedIndex = 1;
    }

    // ============ Crear plugin (mismo ScaffoldService que `lsplugin init`) ============

    private void OnCreateModelChanged(object? sender, SelectionChangedEventArgs args) => OnCreateCompose(sender, null!);

    private void OnCreateCompose(object? sender, TextChangedEventArgs args)
    {
        if (PluginTemplates.FindOrNull(CreateTemplate.SelectedItem as string ?? PluginTemplates.All[0].Id) is not { } template)
            return;
        if (!ReferenceEquals(sender, CreateId) && CreateName.Text is { Length: > 0 } name)
        {
            var clean = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (clean.Length > 0)
                CreateId.Text = "com.empresa." + clean;
        }
        CreateTemplateInfo.Text = $"{template.DisplayName}: {template.Description}";
        CreatePermissions.Text = "Permisos que mostrará LiveStream al usuario: "
            + string.Join(" · ", template.Permissions.Select(p => $"{p} ({PluginPermissions.Descriptions.GetValueOrDefault(p, p)})"));
    }

    private async void OnCreateChooseDir(object? sender, RoutedEventArgs args)
    {
        if (await ChooseFolderAsync() is { } folder)
            CreateDir.Text = folder;
    }

    private void OnCreateRun(object? sender, RoutedEventArgs args)
    {
        var templateId = CreateTemplate.SelectedItem as string ?? PluginTemplates.All[0].Id;
        var id = Trim(CreateId);
        var name = Trim(CreateName);
        Post("Crear plugin", async () =>
        {
            var scaffold = new ScaffoldService().Create(
                Trim(CreateDir),
                new ScaffoldRequest(
                    TemplateId: templateId,
                    Id: id,
                    Name: name,
                    Version: string.IsNullOrWhiteSpace(CreateVersion.Text) ? "0.1.0" : Trim(CreateVersion),
                    MinAppVersion: string.IsNullOrWhiteSpace(CreateMinApp.Text) ? "0.9.0" : Trim(CreateMinApp),
                    Author: Trim(CreateAuthor)));
            Report($"Proyecto {scaffold.Manifest.Id} creado en {scaffold.Directory}. Después: pestaña «Paquete» (compilar → empaquetar).");
        });
    }
}
