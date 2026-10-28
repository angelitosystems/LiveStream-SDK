using System.IO.Pipes;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

namespace LiveStream.SdkCore.Tests;

/// <summary>
/// <see cref="PluginClient"/> frente a un host simulado con <see cref="JsonRpcPeer"/> sobre tuberías en memoria:
/// initialize con ajustes, eventos, llamadas a herramientas y shutdown.
/// </summary>
public sealed class PluginClientTests : IAsyncDisposable
{
    private readonly AnonymousPipeServerStream _toPlugin = new(PipeDirection.Out);
    private readonly AnonymousPipeServerStream _toHost = new(PipeDirection.Out);
    private readonly JsonRpcPeer _host;
    private readonly PluginClient _client;
    private readonly List<JsonNode?> _toolCalls = [];

    public PluginClientTests()
    {
        var pluginIn = new AnonymousPipeClientStream(PipeDirection.In, _toPlugin.ClientSafePipeHandle);
        var hostIn = new AnonymousPipeClientStream(PipeDirection.In, _toHost.ClientSafePipeHandle);
        _client = new PluginClient(new StreamReader(pluginIn), new StreamWriter(_toHost) { AutoFlush = true });
        _host = new JsonRpcPeer(new StreamReader(hostIn), new StreamWriter(_toPlugin) { AutoFlush = true })
        {
            OnRequest = (method, parameters, _) =>
            {
                Assert.Equal(PluginProtocol.ToolsCall, method);
                lock (_toolCalls)
                    _toolCalls.Add(parameters?.DeepClone());
                return Task.FromResult<JsonNode?>(parameters?["name"]?.GetValue<string>() switch
                {
                    "get_studio_state" => new JsonObject { ["content"] = """{"graphics":[{"name":"Marcador"}]}""" },
                    _ => new JsonObject { ["content"] = "ok", ["summary"] = "hecho" },
                });
            },
        };
        _ = _host.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InitializeGuardaLosAjustes_YShutdownTerminaRunAsync()
    {
        var applied = new TaskCompletionSource();
        _client.SettingsApplied = () => { applied.TrySetResult(); return Task.CompletedTask; };
        var running = _client.RunAsync();

        await _host.RequestAsync(PluginProtocol.Initialize,
            new JsonObject { ["settings"] = new JsonObject { ["name"] = "  Hola ", ["n"] = "250", ["bad"] = "x" } }, CancellationToken.None);
        await applied.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Hola", _client.Setting("name"));
        Assert.Equal("def", _client.Setting("missing", "def"));
        Assert.Equal(100, _client.Number("n", 1, 0, 100));
        Assert.Equal(1, _client.Number("bad", 1, 0, 100));

        await _host.RequestAsync(PluginProtocol.Shutdown, null, CancellationToken.None);
        await running.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_client.Stopping.IsCancellationRequested);
    }

    [Fact]
    public async Task MetodoDesconocido_DevuelveMethodNotFound()
    {
        _ = _client.RunAsync();
        var error = await Assert.ThrowsAsync<JsonRpcException>(() => _host.RequestAsync("nope", null, CancellationToken.None));
        Assert.Equal(JsonRpcException.MethodNotFound, error.Code);
    }

    [Fact]
    public async Task LosEventosLleganAlManejador_QuePuedeLlamarHerramientas()
    {
        var done = new TaskCompletionSource<ToolResult>();
        _client.EventReceived = async name =>
        {
            Assert.Equal(PluginProtocol.StudioChangedEvent, name);
            done.TrySetResult(await _client.CallToolAsync("set_graphic_field", new JsonObject { ["x"] = 1 }));
        };
        _ = _client.RunAsync();

        await _host.NotifyAsync(PluginProtocol.Event, new JsonObject { ["name"] = PluginProtocol.StudioChangedEvent }, CancellationToken.None);
        var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new ToolResult("ok", false, "hecho"), result);
        Assert.Equal(1, _toolCalls.Single()!["arguments"]!["x"]!.GetValue<int>());
    }

    [Fact]
    public async Task EnsureGraphic_SoloCreaSiNoExisteUnoConEseNombre()
    {
        _ = _client.RunAsync();

        Assert.False(await _client.EnsureGraphicAsync("marcador", "scoreboard"));
        Assert.True(await _client.EnsureGraphicAsync("Titulares", "headline", PluginClient.Fields(("headline", "Uno"))));

        var create = _toolCalls.Last()!;
        Assert.Equal("create_graphic", create["name"]!.GetValue<string>());
        Assert.Equal("Titulares", create["arguments"]!["name"]!.GetValue<string>());
        Assert.Equal("Uno", create["arguments"]!["fields"]![0]!["value"]!.GetValue<string>());
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _toPlugin.Dispose();
        _toHost.Dispose();
    }
}
