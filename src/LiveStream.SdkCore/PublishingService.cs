using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

namespace LiveStream.SdkCore;

/// <summary>Entrada del marketplace: una versión publicada (o retirada) de un paquete.</summary>
public sealed record CatalogItem(string Id, string Version, bool Yanked, string PublishedAt);

/// <summary>
/// Publicación owner-only al marketplace (token de administración en la variable de entorno, nunca en la línea de
/// órdenes). La CLI, la GUI y el backend existente (`LiveStream-Marketplace`, rutas `v1/admin/…`) hablan este
/// mismo servicio.
/// </summary>
public sealed class PublishingService
{
    public const string TokenVariable = "LS_MARKETPLACE_TOKEN";

    public PublishingService(string serverUrl, string? token, HttpClient? http = null)
    {
        Server = new Uri(serverUrl.TrimEnd('/') + "/");
        if (Server.Scheme != Uri.UriSchemeHttps && !Server.IsLoopback)
            throw new SdkStepException("El servidor del marketplace debe usar HTTPS.",
                "Revisa la URL: p. ej. https://marketplace.livestream.example (solo se permite HTTP en localhost/red de pruebas).");
        Token = token ?? throw new SdkStepException($"Falta el token de administración del marketplace.",
            $"Define la variable de entorno {TokenVariable} antes de publicar (nunca en la línea de órdenes).");
        _http = http ?? new HttpClient { BaseAddress = Server, Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    public Uri Server { get; }
    public string Token { get; }

    private readonly HttpClient _http;

    /// <summary>Síntoma del entorno: null si no hay token; sirve para `doctor` sin contar secretos.</summary>
    public static string? TokenFromEnvironment() =>
        Environment.GetEnvironmentVariable(TokenVariable) is { Length: > 0 } token ? token : null;

    /// <summary>Publica un paquete con su firma (propiedad del propietario; el backend valida el token).</summary>
    public async Task<string> PublishAsync(string packagePath)
    {
        PluginPackage.Inspect(packagePath);
        var signatureText = File.ReadAllText(packagePath + PackageSignature.FileSuffix);
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(File.OpenRead(packagePath)), "package", Path.GetFileName(packagePath) },
            { new StringContent(signatureText), "signature" },
        };
        using var response = await _http.PostAsync("v1/admin/packages", form);
        return await EnsureSpan(response);
    }

    public Task<string> YankAsync(string id, string version) => Mutation("yank", id, version);

    public Task<string> UnyankAsync(string id, string version) => Mutation("unyank", id, version);

    public async Task<IReadOnlyList<CatalogItem>> ListAsync()
    {
        var body = await GetAsync("v1/admin/packages");
        return JsonNode.Parse(body)!["entries"]!.AsArray()
            .Select(e => new CatalogItem(
                e!["id"]!.GetValue<string>(),
                e["version"]!.GetValue<string>(),
                e["yanked"]!.GetValue<bool>(),
                e["publishedAt"]!.GetValue<string>()))
            .ToList();
    }

    public async Task<string> BackupAsync(string output)
    {
        using var response = await _http.GetAsync("v1/admin/backup");
        var body = await EnsureCondensed(response);
        await using (var file = File.Create(output))
            await response.Content.CopyToAsync(file);
        return body is { Length: > 0 } ? body : "Copia descargada correctamente.";
    }

    private async Task<string> GetAsync(string path)
    {
        using var response = await _http.GetAsync(path);
        return await EnsureSpan(response);
    }

    private async Task<string> Mutation(string action, string id, string version)
    {
        var path = $"v1/admin/packages/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(version)}/{action}";
        using var response = await _http.PostAsync(path, null);
        return await EnsureSpan(response);
    }

    private static async Task<string> EnsureSpan(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync()
            : throw new SdkStepException($"El marketplace respondió {(int)response.StatusCode}.",
                Tip(response));

    private static async Task<string> EnsureCondensed(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
            ? await response.Content.ReadAsStringAsync()
            : throw new SdkStepException($"La copia de seguridad no se pudo descargar: código {(int)response.StatusCode}.",
                Tip(response));

    private static string Tip(HttpResponseMessage response)
    {
        var tip = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "El token de la variable LS_MARKETPLACE_TOKEN no es válido.",
            HttpStatusCode.Forbidden => "El token no tiene permisos de administración (solo el propietario publica).",
            HttpStatusCode.Conflict => "Ya existe esa versión del plugin: sube la versión en plugin.json.",
            _ => "Revisa la URL del servidor --server.",
        };
        return tip;
    }
}
