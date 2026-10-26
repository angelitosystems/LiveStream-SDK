using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// __NAME__ - herramientas para LiveStream. Cada N segundos pide el informe de rendimiento (fps del programa,
// uso de CPU/GPU/RAM) y lo escribe en el registro de LiveStream. Requiere studio.read (plugin.json).
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

var peer = new JsonRpcPeer(Console.In, Console.Out);
var shutdown = new TaskCompletionSource();
var seconds = 30.0;

peer.OnRequest = async (method, parameters, ct) =>
{
    switch (method)
    {
        case PluginProtocol.Initialize:
        {
            if (parameters?["settings"]?["seconds"] is { } raw
                && double.TryParse(raw.GetValue<string>(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed is >= 5 and <= 600)
                seconds = parsed;
            _ = RunAsync();
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

// El bucle vive hasta el shutdown (o hasta que cierre stdin): nunca bloquea las respuestas del protocolo.
var worker = RunAsync();
await Task.WhenAny(reading, shutdown.Task, worker);
return 0;

async Task RunAsync()
{
    while (true)
    {
        try
        {
            var result = await peer.RequestAsync(PluginProtocol.ToolsCall,
                new JsonObject { ["name"] = "get_performance_report", ["arguments"] = new JsonObject() },
                CancellationToken.None);
            if (shutdown.Task.IsCompleted)
                return;
            await LogAsync(result?["content"]?.GetValue<string>() ?? "informe vacío", CancellationToken.None);
        }
        catch (Exception ex) when (ex is JsonRpcException or IOException)
        {
            Console.Error.WriteLine($"No se pudo pedir el informe: {ex.Message}");
            return;
        }
        var finished = await Task.WhenAny(shutdown.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
        if (finished == shutdown.Task)
            return;
    }
}

Task LogAsync(string message, CancellationToken ct) =>
    peer.NotifyAsync(PluginProtocol.Log, new JsonObject { ["level"] = "info", ["message"] = message }, ct);
