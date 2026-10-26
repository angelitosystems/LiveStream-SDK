using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// __NAME__ - integración con un webhook para LiveStream. Cada vez que cambia el estudio (evento «studio.changed»)
// lee el estado y lo envía por HTTP POST a la URL de los ajustes. Requiere studio.read (plugin.json).
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);

var peer = new JsonRpcPeer(Console.In, Console.Out);
var shutdown = new TaskCompletionSource();
var url = "";
bool announce = true;
var sending = new SemaphoreSlim(1, 1);

peer.OnRequest = async (method, parameters, ct) =>
{
    switch (method)
    {
        case PluginProtocol.Initialize:
        {
            url = parameters?["settings"]?["url"]?.GetValue<string>() ?? url;
            announce = (parameters?["settings"]?["announce"]?.GetValue<string>() ?? "true") == "true";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var target)
                || target.Scheme is not ("http" or "https"))
                await LogAsync("Ajusta «url» en los ajustes del plugin (http o https).", ct);
            else
                await LogAsync($"Reportará el estado del estudio a {target.Host}.", ct);
            return new JsonObject();
        }
        case PluginProtocol.SettingsChanged:
            url = parameters?["settings"]?["url"]?.GetValue<string>() ?? url;
            return new JsonObject();
        case PluginProtocol.Shutdown:
            shutdown.TrySetResult();
            return new JsonObject();
        default:
            throw new JsonRpcException(JsonRpcException.MethodNotFound, $"Método desconocido: {method}");
    }
};

peer.OnNotification = (method, parameters) =>
{
    if (method == PluginProtocol.Event && parameters?["name"]?.GetValue<string>() == PluginProtocol.StudioChangedEvent)
        _ = ReportAsync();
};

var reading = peer.RunAsync(CancellationToken.None);
await Task.WhenAny(reading, shutdown.Task);
return 0;

async Task ReportAsync()
{
    if (string.IsNullOrWhiteSpace(url) || !announce)
        return;
    // Un envío cada vez; si el estudio cambia más rápido que el webhook, se pierde el resto (no colas).
    if (!await sending.WaitAsync(0))
        return;
    try
    {
        var state = await peer.RequestAsync(PluginProtocol.ToolsCall,
            new JsonObject { ["name"] = "get_studio_state", ["arguments"] = new JsonObject() }, CancellationToken.None);
        using var http = new HttpClient();
        var payload = state?["content"]?.GetValue<string>() ?? "{}";
        await http.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"));
        await LogAsync("Estado enviado al webhook.", CancellationToken.None);
    }
    catch (Exception ex) when (ex is JsonRpcException or IOException or HttpRequestException or JsonException)
    {
        Console.Error.WriteLine($"No se pudo enviar el estado: {ex.Message}");
    }
    finally
    {
        sending.Release();
    }
}

Task LogAsync(string message, CancellationToken ct) =>
    peer.NotifyAsync(PluginProtocol.Log, new JsonObject { ["level"] = "info", ["message"] = message }, ct);
