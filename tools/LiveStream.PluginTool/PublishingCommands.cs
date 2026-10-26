using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkTool;

/// <summary>
/// Órdenes de quien publica: claves, firma, verificación e integración con el marketplace
/// (publicación owner-only; el token de administración lee la CLI de la variable de entorno).
/// </summary>
public static class PublishingCommands
{
    public static int Keygen(Ui ui, string[] rest)
    {
        var pair = SigningService.Keygen(Args.Option(rest, "--private"), Args.Option(rest, "--public"),
            force: Args.Has(rest, "--force"));
        ui.JsonOut(new { privateKey = pair.PrivatePath, publicKey = pair.PublicPath });
        ui.Ok($"Clave privada: {pair.PrivatePath}");
        ui.Line("Haga copia de seguridad de la clave privada y no la subas a ningún repositorio.");
        ui.Line($"Clave pública: {pair.PublicPath}");
        return ExitCodes.Ok;
    }

    public static int Sign(Ui ui, string[] rest)
    {
        var package = Path.GetFullPath(Args.Positional(rest, 0,
            "Ejemplo: lsplugin sign .\\paquete.lsplugin --key %USERPROFILE%\\.livestream-plugins\\private.pem"));
        if (!File.Exists(package))
            throw new SdkStepException($"No existe el paquete: {package}");
        var signature = SigningService.Sign(package, Args.Option(rest, "--key",
            usage: "Uso: lsplugin sign <paquete> --key <private.pem>")
            ?? throw new UsageException("Falta --key.", "Uso: lsplugin sign <paquete> --key <private.pem>"));
        ui.JsonOut(new { path = package + PackageSignature.FileSuffix, id = signature.Id, version = signature.Version, sha256 = signature.Sha256 });
        ui.Ok($"Firmado: {package}{PackageSignature.FileSuffix} ({signature.Id} {signature.Version})");
        ui.Warn("Antes de publicar: comprueba la firma con `lsplugin verify <paquete> --public <publica.pem>`.");
        return ExitCodes.Ok;
    }

    public static int Verify(Ui ui, string[] rest)
    {
        var package = Path.GetFullPath(Args.Positional(rest, 0,
            "Ejemplo: lsplugin verify .\\paquete.lsplugin --public plugin-public-key.pem"));
        var signature = SigningService.Verify(package, Args.Option(rest, "--public",
            usage: "Uso: lsplugin verify <paquete> --public <plugin-public-key.pem>")
            ?? throw new UsageException("Falta --public.", "Uso: lsplugin verify <paquete> --public <plugin-public-key.pem>"));
        ui.JsonOut(new { id = signature.Id, version = signature.Version, sha256 = signature.Sha256 });
        ui.Ok($"Firma válida: {signature.Id} {signature.Version} (paquete {signature.Sha256[0..12]}…)");
        return ExitCodes.Ok;
    }

    public static async Task<int> PublishAsync(Ui ui, string[] rest)
    {
        var package = Path.GetFullPath(Args.Positional(rest, 0,
            "Ejemplo: lsplugin publish .\\paquete.lsplugin [--server https://…]" +
            " (token: variable LS_MARKETPLACE_TOKEN, nunca en la línea de órdenes)"));
        if (!File.Exists(package))
            throw new SdkStepException($"No existe el paquete: {package}");
        var server = Args.ServerOr(rest, ui);
        var publishing = new PublishingService(server, PublishingService.TokenFromEnvironment());
        // Publicación owner-only el propio backend la verifica con el token Bearer desde la env var.
        var manifest = PluginPackage.Inspect(package);
        ui.JsonOut(new
        {
            package,
            id = manifest.Id,
            version = manifest.Version,
            server = publishing.Server.ToString(),
            signature = package + PackageSignature.FileSuffix,
        });
        ui.Line($"Publicando {manifest.Id} {manifest.Version} en {publishing.Server}…");
        var body = await publishing.PublishAsync(package);
        ui.Ok($"Publicado {manifest.Id} {manifest.Version} en {publishing.Server}");
        ui.Detail(body);
        return ExitCodes.Ok;
    }

    public static async Task<int> YankAsync(Ui ui, string[] rest, bool unyank)
    {
        var positional = Args.PositionalList(rest);
        if (positional.Count < 2)
            throw new UsageException("Faltan el id del plugin y la versión.",
                $"Uso: lsplugin {(unyank ? "unyank" : "yank")} <id> <versión> [--server https://…]");
        var server = Args.ServerOr(rest, ui);
        var publishing = new PublishingService(server, PublishingService.TokenFromEnvironment());
        ui.JsonOut(new { id = positional[0], version = positional[1], action = unyank ? "unyank" : "yank", server = publishing.Server.ToString() });
        if (!unyank && !Args.Has(rest, "--yes", "-y")
            && !ui.Confirm($"¿Retirar la versión {positional[1]} de {positional[0]} del catálogo?", assumeDefault: false))
        {
            ui.Line("No retirado.");
            return ExitCodes.Error;
        }
        var body = await (unyank ? publishing.UnyankAsync(positional[0], positional[1]) : publishing.YankAsync(positional[0], positional[1]));
        ui.Ok(body);
        return ExitCodes.Ok;
    }

    public static async Task<int> BackupAsync(Ui ui, string[] rest)
    {
        var server = Args.ServerOr(rest, ui);
        var publishing = new PublishingService(server, PublishingService.TokenFromEnvironment());
        var output = Path.GetFullPath(Args.Option(rest, "-o", null, "marketplace-backup.zip") ?? "marketplace-backup.zip");
        ui.JsonOut(new { server = publishing.Server.ToString(), output });
        ui.Line($"Descargando respaldo de {publishing.Server} …");
        var body = await publishing.BackupAsync(output);
        ui.Ok(body);
        ui.Detail($"Copia guardada en {output}");
        return ExitCodes.Ok;
    }
}
