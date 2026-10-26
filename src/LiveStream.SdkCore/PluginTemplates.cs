using System.Text;
using System.Text.Json;
using LiveStream.PluginSdk;

namespace LiveStream.SdkCore;

/// <summary>Definición de una plantilla del scaffold (`lsplugin init`).</summary>
public sealed record PluginTemplate(
    string Id,
    string DisplayName,
    string Description,
    string Category,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PluginSetting> Settings);

/// <summary>
/// Catálogo de plantillas que comparten la CLI y la GUI. Cada una genera un proyecto que compila y llama a
/// herramientas reales del protocolo v1 (verificado junto con la CLI). El código va incrustado en
/// <c>Templates/</c> para que funcione igual con el SDK instalado que desde el repositorio.
/// </summary>
public static class PluginTemplates
{
    public static IReadOnlyList<PluginTemplate> All { get; } =
    [
        new("basic", "Básico",
            "Punto de partida mínimo: saluda con los ajustes y cuenta las escenas del estudio.",
            "Ejemplos", [PluginPermissions.StudioRead],
            [new PluginSetting("greeting", "Saludo", Default: "Hola")]),
        new("graphics", "Gráficos",
            "Crea un gráfico (lower_third) al arrancar y lo lanza al aire cuando cambia el estudio.",
            "Gráficos", [PluginPermissions.StudioRead, PluginPermissions.StudioModify],
            [new PluginSetting("graphic", "Nombre del gráfico", Default: "titulo-inicio"),
             new PluginSetting("title", "Título", Default: "")]),
        new("sources", "Fuentes",
            "Lee las escenas y añade una fuente de texto editable desde los ajustes del plugin.",
            "Fuentes", [PluginPermissions.StudioRead, PluginPermissions.StudioModify],
            [new PluginSetting("scene", "Escena destino", Default: "Escena 1"),
             new PluginSetting("source", "Nombre de la fuente", Default: "Aviso"),
             new PluginSetting("text", "Texto", Default: "")]),
        new("filters", "Filtros de audio",
            "Añade un filtro de audio (noise_suppression, noise_gate, compressor, limiter, gain) a una fuente.",
            "Audio", [PluginPermissions.StudioModify],
            [new PluginSetting("source", "Fuente de audio", Default: "Micrófono"),
             new PluginSetting("filter", "Filtro", Default: "noise_suppression")]),
        new("integrations", "Integraciones",
            "Envía el estado del estudio a un webhook (HTTP POST) de un servicio externo.",
            "Integraciones", [PluginPermissions.StudioRead],
            [new PluginSetting("url", "URL del webhook", Default: ""),
             new PluginSetting("announce", "Enviar (true/false)", Default: "true")]),
        new("tools", "Herramientas",
            "Informe periódico de rendimiento (fps, CPU/GPU/RAM) en el registro de LiveStream.",
            "Herramientas", [PluginPermissions.StudioRead],
            [new PluginSetting("seconds", "Segundos entre informes", Default: "30")]),
    ];

    public static PluginTemplate Find(string id) =>
        All.FirstOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Plantilla desconocida: «{id}». Válidas: {string.Join(", ", All.Select(t => t.Id))}.");

    public static PluginTemplate FindOrNull(string id) => All.FirstOrDefault(t => t.Id == id, All[0]);

    /// <summary>Contenido textual de un archivo de plantilla incrustado (id vacío = común).</summary>
    public static string Raw(string templateId, string file)
    {
        var name = string.IsNullOrEmpty(templateId) ? $"common.{file}" : $"{templateId}.{file}";
        using var stream = typeof(PluginTemplates).Assembly.GetManifestResourceStream(name)
            ?? throw new IOException($"Falta la plantilla incrustada «{name}» (instalación del SDK incompleta).");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>Pedido de creación de un proyecto de plugin.</summary>
public sealed record ScaffoldRequest(
    string TemplateId,
    string Id,
    string Name,
    string Version = "0.1.0",
    string MinAppVersion = "0.9.0",
    string Author = "",
    string Description = "",
    string ProjectName = "");

/// <summary>Resultado del scaffold: lo que hizo falta escribir para que `dotnet publish` funcione.</summary>
public sealed record ScaffoldResult(PluginManifest Manifest, string Directory, string ProjectPath, string ProjectName);
