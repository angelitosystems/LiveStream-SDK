using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LiveStream.SdkCore;

namespace LiveStream.SdkStudio;

public partial class MainWindow : Window
{
    private readonly DoctorService _doctor = new();
    private string? _manifestPath;
        private readonly ObservableCollection<CheckRow> _homeChecks = [];
    private readonly ObservableCollection<PluginRow> _plugins = [];

    /// <summary>Registro compartido de resultados (cue de abajo en la ventana).</summary>
    private void Report(string text) => Log.Text = text;

    /// <summary>Acción de página: no bloquea la UI y cualquier error es accionable en el registro.</summary>
    private async void Post(string heading, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Report($"{heading}: no se pudo — {ex.Message}");
        }
    }

    /// <summary>Selector de carpeta (diálogos nativos de Avalonia; cancelar devuelve null y no hace nada).</summary>
    private async Task<string?> ChooseFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Elige la carpeta",
        });
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    /// <summary>Selector de archivo: plugin.json (o paquete .lsplugin con <paramref name="package"/>).</summary>
    private async Task<string?> ChooseFileAsync(bool package)
    {
        var types = new List<FilePickerFileType>();
        if (package)
        {
            types.Add(new FilePickerFileType("Paquete de LiveStream") { Patterns = ["*.lsplugin"] });
        }
        types.Add(new FilePickerFileType("plugin.json") { Patterns = ["plugin.json"] });
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Elige archivo",
            FileTypeFilter = types,
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <summary>Abre una carpeta en el explorador; nunca ejecuta código del plugin instalado.</summary>
    private static void OpenFolder(string path)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        });
    }

    private static string Trim(TextBox box) => box.Text?.Trim() ?? string.Empty;
}

/// <summary>Fila del panel de diagnóstico/Inicio: semáforo + descripción accionable.</summary>
public sealed record CheckRow(bool Ok, string Name, string Detail, string? Hint)
{
    public IBrush MarkBrush => Ok ? Brush.Parse("#FF30A46C") : Brush.Parse("#FFF5A524");
    public string Caption => Ok ? "●" : "○";
    public string TextOfRow => Hint is { Length: > 0 } hint
        ? $"{Name}: {Detail}  →  {hint}"
        : $"{Name}: {Detail}";
}

/// <summary>Fila de la lista de plugins instalados.</summary>
public sealed record PluginRow(string Id, string Name, string Version, bool Enabled, bool Signed, string Directory, string Source)
{
    public string Caption => $"{Name} {Version}";
    public string PathText => $" {Id} · {(Enabled ? "activo" : "inactivo")}{(Signed ? "" : " · sin firma")}";
    public IBrush StateBrush => Enabled ? Brush.Parse("#FF30A46C") : Brush.Parse("#FF9AA1AB");
}
