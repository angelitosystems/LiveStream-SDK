using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// __NAME__ - filtros de audio para LiveStream. Al arrancar añade el filtro de los ajustes al final de la cadena de la
// fuente indicada (p. ej. supresión de ruido para un micrófono). Filtros válidos: noise_suppression, noise_gate,
// compressor, limiter, gain. Requiere studio.modify (plugin.json).
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

var peer = new JsonRpcPeer(Console.In, Console.Out);
var shutdown = new TaskCompletionSource();
var source = "Micrófono";
var filter = "noise_suppression";

peer.OnRequest = async (method, parameters, ct) =>
{
    switch (method)
    {
        case PluginProtocol.Initialize:
        {
            source = parameters?["settings"]?["source"]?.GetValue<string>() ?? source;
            filter = parameters?["settings"]?["filter"]?.GetValue<string>() ?? filter;
            // add_audio_filter crea el filtro con valores por defecto razonables; el usuario los ajusta luego en la app.
            await peer.RequestAsync(PluginProtocol.ToolsCall, new JsonObject
            {
                ["name"] = "add_audio_filter",
                ["arguments"] = new JsonObject { ["source"] = source, ["filter"] = filter },
            }, ct);
            await LogAsync($"Filtro «{filter}» añadido a la fuente «{source}».", ct);
            return new JsonObject();
        }
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
