using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// __NAME__ - gráficos para LiveStream. Al arrancar crea una lower_third con el título de los ajustes; al recibir
// «studio.changed» la muestra con animación. Requiere studio.read y studio.modify (plugin.json).
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

var peer = new JsonRpcPeer(Console.In, Console.Out);
var shutdown = new TaskCompletionSource();
var graphicName = "titulo-infercio";

peer.OnRequest = async (method, parameters, ct) =>
{
    switch (method)
    {
        case PluginProtocol.Initialize:
        {
            graphicName = parameters?["settings"]?["graphic"]?.GetValue<string>() ?? graphicName;
            var title = parameters?["settings"]?["title"]?.GetValue<string>() ?? "__NAME__";
            // create_graphic queda oculto hasta show_graphic; los campos omitidos usan sus valores por defecto.
            await CallAsync("create_graphic", new JsonObject
            {
                ["kind"] = "lower_third",
                ["name"] = graphicName,
                ["fields"] = new JsonObject { ["name"] = graphicName, ["title"] = title },
            }, ct);
            await LogAsync($"Gráfico «{graphicName}» listo (oculto).", ct);
            return new JsonObject();
        }
        case PluginProtocol.SettingsChanged:
            // create_graphic con el mismo nombre actualiza sus campos y conserva el estado de visibility.
            graphicName = parameters?["settings"]?["graphic"]?.GetValue<string>() ?? graphicName;
            return new JsonObject();
        case PluginProtocol.Shutdown:
            // Sale limpio: daño cero al estudio.
            await CallAsync("delete_graphic", new JsonObject { ["graphic"] = graphicName }, CancellationToken.None);
            shutdown.TrySetResult();
            return new JsonObject();
        default:
            throw new JsonRpcException(JsonRpcException.MethodNotFound, $"Método desconocido: {method}");
    }
};

peer.OnNotification = (method, parameters) =>
{
    if (method == PluginProtocol.Event && parameters?["name"]?.GetValue<string>() == PluginProtocol.StudioChangedEvent)
        _ = ShowAsync();
};

var reading = peer.RunAsync(CancellationToken.None);
await Task.WhenAny(reading, shutdown.Task);
return 0;

async Task ShowAsync()
{
    try
    {
        await CallAsync("show_graphic", new JsonObject { ["graphic"] = graphicName }, CancellationToken.None);
    }
    catch (Exception ex) when (ex is JsonRpcException or IOException)
    {
        Console.Error.WriteLine($"No se pudo mostrar el gráfico: {ex.Message}");
    }
}

async Task CallAsync(string tool, JsonNode arguments, CancellationToken ct)
{
    await peer.RequestAsync(PluginProtocol.ToolsCall,
        new JsonObject { ["name"] = tool, ["arguments"] = arguments }, ct);
}

Task LogAsync(string message, CancellationToken ct) =>
    peer.NotifyAsync(PluginProtocol.Log, new JsonObject { ["level"] = "info", ["message"] = message }, ct);
