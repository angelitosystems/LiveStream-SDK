using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// __NAME__ - fuentes para LiveStream. Al arrancar lista las escenas con sus fuentes y crea una fuente de texto con el
// texto de los ajustes (si no existe ya). Requiere studio.read y studio.modify (plugin.json).
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

var peer = new JsonRpcPeer(Console.In, Console.Out);
var shutdown = new TaskCompletionSource();
var sceneName = "Escena 1";
var textSource = "Aviso";

peer.OnRequest = async (method, parameters, ct) =>
{
    switch (method)
    {
        case PluginProtocol.Initialize:
        {
            sceneName = parameters?["settings"]?["scene"]?.GetValue<string>() ?? sceneName;
            textSource = parameters?["settings"]?["source"]?.GetValue<string>() ?? textSource;
            var text = parameters?["settings"]?["text"]?.GetValue<string>() ?? "Edita el texto en los ajustes del plugin.";
            await ReportScenesAsync(ct);
            // add_source coloca la fuente arriba del resto de la escena; kind=text pide el texto inicial.
            await CallAsync("add_source", new JsonObject
            {
                ["scene"] = sceneName,
                ["kind"] = "text",
                ["name"] = textSource,
                ["text"] = text,
            }, ct);
            await LogAsync($"Fuente de texto «{textSource}» en «{sceneName}».", ct);
            return new JsonObject();
        }
        case PluginProtocol.SettingsChanged:
            // set_text afecta a todas las escenas donde aparece la fuente.
            if (parameters?["settings"]?["text"] is { } update)
                _ = CallAsync("set_text", new JsonObject { ["source"] = textSource, ["text"] = update.GetValue<string>() },
                    CancellationToken.None);
            return new JsonObject();
        case PluginProtocol.Shutdown:
            shutdown.TrySetResult();
            return new JsonObject();
        default:
            throw new JsonRpcException(JsonRpcException.MethodNotFound, $"Método desconocido: {method}");
    }
};

var reading = peer.RunAsync(CancellationToken.None);
await Task.WhenAny(reading, shutdown.Task);
return 0;

async Task ReportScenesAsync(CancellationToken ct)
{
    try
    {
        var result = await peer.RequestAsync(PluginProtocol.ToolsCall,
            new JsonObject { ["name"] = "get_studio_state", ["arguments"] = new JsonObject() }, ct);
        var state = JsonNode.Parse(result?["content"]?.GetValue<string>() ?? "{}");
        foreach (var scene in state?["scenes"]?.AsArray() ?? [])
            await LogAsync($"Escena: {scene?["name"]}", ct);
    }
    catch (Exception ex) when (ex is JsonRpcException or IOException or JsonException)
    {
        Console.Error.WriteLine($"No se pudo leer el estado: {ex.Message}");
    }
}

async Task CallAsync(string tool, JsonNode arguments, CancellationToken ct)
{
    await peer.RequestAsync(PluginProtocol.ToolsCall,
        new JsonObject { ["name"] = tool, ["arguments"] = arguments }, ct);
}

Task LogAsync(string message, CancellationToken ct) =>
    peer.NotifyAsync(PluginProtocol.Log, new JsonObject { ["level"] = "info", ["message"] = message }, ct);
