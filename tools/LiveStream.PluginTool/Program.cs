using System.Net.Http.Headers;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using LiveStream.PluginSdk;

// lsplugin: herramienta del publicador. El token de administración se lee de LS_MARKETPLACE_TOKEN (nunca de la línea
// de órdenes, para que no quede en el historial) y la clave privada nunca sale de este equipo.
const string TokenVariable = "LS_MARKETPLACE_TOKEN";

try
{
    return await RunAsync(args);
}
catch (Exception ex) when (ex is PluginValidationException or IOException or HttpRequestException or ArgumentException or UnauthorizedAccessException or CryptographicException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static async Task<int> RunAsync(string[] args)
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        Console.WriteLine("""
            lsplugin — herramienta de plugins de LiveStream

              new <carpeta> --id <com.empresa.plugin> --name <nombre>   Crea plugin.json de partida
              validate <carpeta|paquete.lsplugin>                      Comprueba manifiesto y paquete
              pack <carpeta> [-o salida.lsplugin]                      Empaqueta (reproducible)
              keygen --private <ruta.pem> --public <ruta.pem> [--force] Crea el par de claves del publicador
              sign <paquete> --key <privada.pem>                       Escribe <paquete>.sig.json
              verify <paquete> --public <publica.pem>                  Comprueba paquete y firma
              publish <paquete> --server <url>                         Sube paquete + firma (token en LS_MARKETPLACE_TOKEN)
              yank|unyank <id> <versión> --server <url>                Retira o restaura una versión
              list --server <url>                                      Lista todas las versiones (incluidas retiradas)
              backup --server <url> -o copia.zip                       Descarga una copia de seguridad de los datos
            """);
        return 0;
    }

    var rest = args[1..];
    switch (args[0])
    {
        case "new":
        {
            var dir = Positional(rest, 0);
            var manifest = new PluginManifest(Option(rest, "--id"), Option(rest, "--name"), "0.1.0", 1, "MiPlugin.exe",
                Author: Option(rest, "--author", ""), Description: "Describe aquí qué hace el plugin.", Category: "General",
                MinAppVersion: "0.9.0", Permissions: [PluginPermissions.StudioRead]);
            if (manifest.Validate() is { } error)
                throw new PluginValidationException(error);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, PluginManifest.FileName);
            if (File.Exists(path))
                throw new IOException($"Ya existe {path}.");
            File.WriteAllText(path, JsonSerializer.Serialize(manifest, PluginManifest.Json));
            Console.WriteLine($"Creado {path}. Copia la estructura de sdk/examples/HelloPlugin para el ejecutable.");
            return 0;
        }

        case "validate":
        {
            var target = Positional(rest, 0);
            var manifest = Directory.Exists(target)
                ? PluginManifest.Parse(File.ReadAllText(Path.Combine(target, PluginManifest.FileName)))
                : PluginPackage.Inspect(target);
            Console.WriteLine($"Válido: {manifest.Id} {manifest.Version} (API {manifest.ApiVersion}, permisos: {string.Join(", ", manifest.PermissionList)})");
            return 0;
        }

        case "pack":
        {
            var dir = Positional(rest, 0);
            var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(dir, PluginManifest.FileName)));
            var output = Option(rest, "-o", $"{manifest.Id}-{manifest.Version}{PluginPackage.Extension}");
            if (File.Exists(output))
                File.Delete(output);
            PluginPackage.Pack(dir, output);
            Console.WriteLine($"Paquete: {output} ({new FileInfo(output).Length / 1024} KB, sha256 {PluginPackage.Sha256(output)})");
            return 0;
        }

        case "keygen":
        {
            var privatePath = Path.GetFullPath(Option(rest, "--private"));
            var publicPath = Path.GetFullPath(Option(rest, "--public"));
            if ((File.Exists(privatePath) || File.Exists(publicPath)) && !rest.Contains("--force"))
                throw new IOException("Ya existe una clave. Usa --force solo si de verdad quieres sustituirla: los plugins firmados con la anterior dejarán de verificarse.");
            if (IsInsideGitRepository(privatePath))
                throw new ArgumentException("La clave privada no puede guardarse dentro de un repositorio git.");
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
            File.WriteAllText(privatePath, key.ExportPkcs8PrivateKeyPem());
            RestrictToCurrentUser(privatePath);
            Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
            File.WriteAllText(publicPath, key.ExportSubjectPublicKeyInfoPem());
            Console.WriteLine($"Clave privada: {privatePath} (haz copia de seguridad; no la subas a ningún sitio)");
            Console.WriteLine($"Clave pública: {publicPath}");
            return 0;
        }

        case "sign":
        {
            var package = Positional(rest, 0);
            var signature = PackageSignature.Create(package, File.ReadAllText(Option(rest, "--key")));
            File.WriteAllText(package + PackageSignature.FileSuffix, signature.ToJson());
            Console.WriteLine($"Firmado: {package}{PackageSignature.FileSuffix} ({signature.Id} {signature.Version})");
            return 0;
        }

        case "verify":
        {
            var package = Positional(rest, 0);
            var signature = PackageSignature.FromJson(File.ReadAllText(package + PackageSignature.FileSuffix));
            if (signature.VerifyPackage(package, File.ReadAllText(Option(rest, "--public"))) is { } error)
                throw new PluginValidationException(error);
            Console.WriteLine($"Firma válida: {signature.Id} {signature.Version}");
            return 0;
        }

        case "publish":
        {
            var package = Positional(rest, 0);
            var signaturePath = package + PackageSignature.FileSuffix;
            PluginPackage.Inspect(package);
            using var form = new MultipartFormDataContent
            {
                { new StreamContent(File.OpenRead(package)), "package", Path.GetFileName(package) },
                { new StringContent(File.ReadAllText(signaturePath)), "signature" },
            };
            var body = await SendAsync(rest, HttpMethod.Post, "v1/admin/packages", form);
            Console.WriteLine($"Publicado: {body}");
            return 0;
        }

        case "yank" or "unyank":
        {
            var body = await SendAsync(rest, HttpMethod.Post, $"v1/admin/packages/{Uri.EscapeDataString(Positional(rest, 0))}/{Uri.EscapeDataString(Positional(rest, 1))}/{args[0]}", null);
            Console.WriteLine(body);
            return 0;
        }

        case "list":
        {
            var body = await SendAsync(rest, HttpMethod.Get, "v1/admin/packages", null);
            foreach (var entry in JsonNode.Parse(body)!["entries"]!.AsArray())
                Console.WriteLine($"{entry!["id"],-40} {entry["version"],-10} {(entry["yanked"]!.GetValue<bool>() ? "RETIRADA" : "publicada")}  {entry["publishedAt"]}");
            return 0;
        }

        case "backup":
        {
            using var http = Client(rest);
            using var response = await http.GetAsync("v1/admin/backup");
            await EnsureAsync(response);
            var output = Option(rest, "-o", $"marketplace-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
            await using (var file = File.Create(output))
                await response.Content.CopyToAsync(file);
            Console.WriteLine($"Copia guardada: {output}");
            return 0;
        }

        default:
            throw new ArgumentException($"Orden desconocida: {args[0]}. Usa «lsplugin help».");
    }
}

static async Task<string> SendAsync(string[] rest, HttpMethod method, string path, HttpContent? content)
{
    using var http = Client(rest);
    using var request = new HttpRequestMessage(method, path) { Content = content };
    using var response = await http.SendAsync(request);
    await EnsureAsync(response);
    return await response.Content.ReadAsStringAsync();
}

static HttpClient Client(string[] rest)
{
    var server = new Uri(Option(rest, "--server").TrimEnd('/') + "/");
    if (server.Scheme != Uri.UriSchemeHttps && !server.IsLoopback)
        throw new ArgumentException("El servidor debe usar HTTPS.");
    var token = Environment.GetEnvironmentVariable(TokenVariable);
    if (string.IsNullOrWhiteSpace(token))
        throw new ArgumentException($"Define la variable de entorno {TokenVariable} con el token de administración.");
    var http = new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromMinutes(5) };
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return http;
}

static async Task EnsureAsync(HttpResponseMessage response)
{
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"El servidor respondió {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
}

static string Positional(string[] rest, int index)
{
    var values = new List<string>();
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i].StartsWith('-'))
        {
            if (rest[i] != "--force")
                i++;
            continue;
        }
        values.Add(rest[i]);
    }
    return index < values.Count ? values[index] : throw new ArgumentException("Faltan argumentos. Usa «lsplugin help».");
}

static string Option(string[] rest, string name, string? fallback = null)
{
    var i = Array.IndexOf(rest, name);
    if (i >= 0 && i + 1 < rest.Length)
        return rest[i + 1];
    return fallback ?? throw new ArgumentException($"Falta {name}.");
}

static bool IsInsideGitRepository(string path)
{
    for (var dir = Path.GetDirectoryName(path); dir is not null; dir = Path.GetDirectoryName(dir))
        if (Directory.Exists(Path.Combine(dir, ".git")))
            return true;
    return false;
}

static void RestrictToCurrentUser(string path)
{
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return;
    }
    var security = new FileSecurity();
    security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
    security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, FileSystemRights.FullControl, AccessControlType.Allow));
    new FileInfo(path).SetAccessControl(security);
}
