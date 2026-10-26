using System.Diagnostics;
using System.Text.Json.Nodes;
using LiveStream.SdkTool;
using LiveStream.SdkCore;

namespace LiveStream.SdkTool;

/// <summary>Órdenes de proyecto y entorno de `lsplugin`: scaffold, plantillas, doctor, validación, build y pack.</summary>
public static partial class CliCommands
{
    public static async Task<int> InitAsync(Ui ui, string[] rest)
    {
        var positional = Args.PositionalList(rest);
        var dir = Path.GetFullPath(positional[0])!;
        var templates = PluginTemplates.All.Select(t => t.Id).ToArray();
        var templateId = Args.Option(rest, "--template", usage: "Elige con --template <id>; lista en: lsplugin init-templates.", PluginTemplates.All[0].Id) ?? PluginTemplates.All[0].Id;
        ValidateTemplate(templateId);
        var id = Args.Option(rest, "--id");
        var name = Args.Option(rest, "--name");
        if (id is null || name is null)
        {
            // Preguntar los dos campos de identidad con valores sugeridos del resto de la orden.
            var suggestion = Path.GetFileNameWithoutExtension(dir);
            id ??= ui.Ask("Id del plugin (com.empresa.plugin)", $"com.empresa.{Suggest(suggestion)}");
            name ??= ui.Ask("Nombre visible del plugin", suggestion);
        }
        var scaffold = new ScaffoldService().Create(dir, new ScaffoldRequest(templateId, id, name,
            Version: Args.Option(rest, "--version") ?? "0.1.0",
            MinAppVersion: Args.Option(rest, "--min-app-version") ?? "0.9.0",
            Author: Args.Option(rest, "--author") ?? "",
            Description: Args.Option(rest, "--description") ?? "",
            ProjectName: Args.Option(rest, "--project") ?? ""));
        ui.JsonOut(new
        {
            manifest = new
            {
                id = scaffold.Manifest.Id,
                name = scaffold.Manifest.Name,
                version = scaffold.Manifest.Version,
                template = templateId,
                entry = scaffold.Manifest.Entry,
                permissions = scaffold.Manifest.PermissionList,
            },
            directory = scaffold.Directory,
            project = scaffold.ProjectPath,
        });
        ui.Warn($"Creado el proyecto de {scaffold.Manifest.Id} «{scaffold.Manifest.Name}» en {scaffold.Directory} (plantilla {templateId}).");
        ui.Line(Console.Out.NewLine + """
            Siguiente paso (desde la carpeta del proyecto):
              lsplugin build                     # o: dotnet publish -c Release
              lsplugin pack bin\Release\net10.0\win-x64\
              lsplugin install .\<paquete>.lsplugin --dev    # prueba sin firma (Modo desarrollador en la app)
            """);
        return ExitCodes.Ok;
    }

    private static string Suggest(string folderName)
    {
        var clean = new string(folderName.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return clean is { Length: > 0 } ? clean.ToLowerInvariant() : "miplugin";
    }

    private static void ValidateTemplate(string id)
    {
        var allowed = PluginTemplates.All.Select(t => t.Id).ToArray();
        if (!allowed.Contains(id))
            throw new UsageException($"Plantilla desconocida: «{id}».", $"Válidas: {string.Join(", ", allowed)}.");
    }

    public static int ListTemplates(Ui ui)
    {
        ui.JsonOut(PluginTemplates.All.Select(t => new
        {
            id = t.Id,
            name = t.DisplayName,
            description = t.Description,
            category = t.Category,
            permissions = t.Permissions,
        }).ToArray());
        ui.Line("Plantillas de `lsplugin init`:");
        foreach (var template in PluginTemplates.All)
            ui.Line($"  {template.Id,-12} {template.DisplayName,-18} {template.Description}");
        return ExitCodes.Ok;
    }

    public static async Task<int> DoctorAsync(Ui ui, string[] rest)
    {
        var checks = await new DoctorService().CheckAsync();
        ui.JsonOut(checks.Select(c => new { name = c.Name, ok = c.Ok, detail = c.Detail, hint = c.Hint, section = c.Section }));
        var broken = 0;
        ui.Line("Diagnóstico del entorno de plugins de LiveStream:");
        foreach (var check in checks)
        {
            var symbol = check.Ok ? "[ok]" : "[!!]";
            if (!check.Ok)
                broken++;
            ui.Line($"  {symbol} {check.Name,-28} {check.Detail}");
            if (!check.Ok && check.Hint is { Length: > 0 })
                ui.Line($"       ↳ {check.Hint}");
        }
        ui.Line(broken == 0 ? "\nTodo listo para crear y publicar plugins." : $"\n{broken} comprobación(es) pendiente(s) — con las sugerencias de arriba, listo.");
        return broken == 0 ? ExitCodes.Ok : ExitCodes.Error;
    }

    public static async Task<int> ValidateAsync(Ui ui, string[] rest)
    {
        var target = Args.Positional(rest, 0, "Ejemplo: lsplugin validate bin\\Release\\net10.0\\win-x64\\  (o un paquete .lsplugin)");
        var manifest = PackagingService.Validate(target);
        ui.JsonOut(new
        {
            id = manifest.Id,
            name = manifest.Name,
            version = manifest.Version,
            apiVersion = manifest.ApiVersion,
            entry = manifest.Entry,
            permissions = manifest.PermissionList,
            dependencies = manifest.Dependencies,
            settings = manifest.Settings?.Select(property => property.Key),
        });
        ui.Ok($"Válido: {manifest.Id} {manifest.Version} (API {manifest.ApiVersion}, permisos: {string.Join(", ", manifest.PermissionList)})");
        return ExitCodes.Ok;
    }

    public static async Task<int> BuildAsync(Ui ui, string[] rest)
    {
        var source = rest.Length > 0 && !rest[0].StartsWith('-') ? rest[0] : ".";
        ui.Line($"Publicando {source}…");
        var result = await BuildService.PublishAsync(source);
        ui.JsonOut(new
        {
            project = result.ProjectPath,
            output = result.OutputDirectory,
            durationMs = result.DurationMs,
            targetFramework = result.TargetFramework,
        });
        ui.Ok($"Publicación lista en {result.OutputDirectory} ({result.DurationMs} ms).");
        ui.Line("Siguiente paso: lsplugin pack \"" + result.OutputDirectory + "\"");
        return ExitCodes.Ok;
    }

    public static async Task<int> PackAsync(Ui ui, string[] rest)
    {
        var dir = Args.Positional(rest, 0, "Ejemplo: lsplugin pack bin\\Release\\net10.0\\win-x64\\ [-o salida.lsplugin]");
        var info = PackagingService.Pack(dir, Args.Option(rest, "-o"));
        ui.JsonOut(new
        {
            path = info.Path,
            size = info.SizeBytes,
            sha256 = info.Sha256,
            id = info.Manifest.Id,
            version = info.Manifest.Version,
            signaturePath = info.SignaturePath,
            signed = info.Signed,
        });
        ui.Ok($"Paquete: {info.Path} ({info.SizeBytes / 1024} KB, sha256 {info.Sha256})");
        if (!info.Signed)
            ui.Warn("El paquete no lleva firma: la app solo lo acepta en Modo desarrollador. Firma con `lsplugin sign`.");
        return ExitCodes.Ok;
    }
}
