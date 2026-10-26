using System.Text.Json;
using LiveStream.PluginSdk;

namespace LiveStream.SdkCore;

/// <summary>
/// Crea el proyecto completo de un plugin: plugin.json, csproj, Program.cs de la plantilla, .gitignore y (con el
/// SDK instalado) NuGet.config apuntando al feed local. Es exactamente el mismo servicio que usan `lsplugin init`
/// y la página «Crear plugin» de la GUI.
/// </summary>
public sealed class ScaffoldService
{
    /// <summary>Deriva el nombre de proyecto .NET (sin espacios) de --project o de la última parte del id.</summary>
    public static string ProjectNameFrom(string projectId, string? requested = null)
    {
        var source = string.IsNullOrWhiteSpace(requested) ? projectId.Split('.')[^1] : requested;
        var clean = new string(source.Where(char.IsLetterOrDigit).ToArray());
        if (clean.Length == 0)
            throw new SdkStepException("No se puede derivar el nombre del proyecto del id.", "Usa --project MiPlugin con un nombre en letras.");
        if (!char.IsLetter(clean[0]))
            clean = "Plugin" + clean;
        return char.ToUpperInvariant(clean[0]) + clean[1..];
    }

    public ScaffoldResult Create(string targetDirectory, ScaffoldRequest request)
    {
        var template = FindTemplate(request.TemplateId);
        ValidateRequest(request);
        var projectName = ProjectNameFrom(request.Id, request.ProjectName);
        // Los plugins se ejecutan en Windows x64: el punto de entrada siempre es un .exe.
        var entry = $"{projectName}.exe";
        ValidateRequest(request);
        var manifest = new PluginManifest(request.Id, request.Name, request.Version, 1, entry,
            Author: request.Author, Description: DescriptionOrDefault(request, template), Category: template.Category,
            MinAppVersion: request.MinAppVersion, Permissions: [.. template.Permissions], Settings: [.. template.Settings]);
        if (manifest.Validate() is { } error)
            throw new SdkStepException($"El manifiesto generado no es válido: {error}");

        var dir = Path.GetFullPath(targetDirectory);
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
            throw new SdkStepException($"La carpeta {dir} ya tiene contenido.", "Apunta a una carpeta nueva o vacía para el proyecto.");
        Directory.CreateDirectory(dir);

        try
        {
            Write(dir, PluginManifest.FileName, JsonSerializer.Serialize(manifest, PluginManifest.Json));
            // La referencia al SDK: con el SDK instalado va por paquete (feed local, sin conexión) y se escribe la
            // NuGet.config; depurando desde el repositorio, por ProjectReference al código fuente.
            if (SdkLocations.LocalFeed() is { } feed)
            {
                Write(dir, $"{projectName}.csproj", PluginTemplates.Raw("", "Project.csproj")
                    .Replace("__SDKREF__", $"<PackageReference Include=\"LiveStream.PluginSdk\" Version=\"{SdkLocations.SdkVersion()}\" />"));
                Write(dir, "NuGet.config", PluginTemplates.Raw("", "NuGet.config").Replace("__SDKDIR__", feed));
            }
            else if (SdkLocations.RepoSdkPath() is { } source)
            {
                Write(dir, $"{projectName}.csproj", PluginTemplates.Raw("", "Project.csproj")
                    .Replace("__SDKREF__", $"<ProjectReference Include=\"{source}\" />"));
            }
            else
            {
                throw new SdkStepException("Falta el SDK de plugins para restaurar el proyecto.",
                    "Instala LiveStream-SDK-Setup (define LIVESTREAM_SDK_DIR) o ejecuta esto desde el repositorio de LiveStream-SDK.");
            }
            Write(dir, "Program.cs", PluginTemplates.Raw(template.Id, "Program.cs").Replace("__NAME__", projectName));
            Write(dir, ".gitignore", PluginTemplates.Raw("", "gitignore"));
            Write(dir, "README.md", PluginTemplates.Raw("", "README.md")
                .Replace("__NAME__", projectName).Replace("__DESC__", template.Description));
            return new ScaffoldResult(manifest, dir, Path.Combine(dir, $"{projectName}.csproj"), projectName);
        }
        catch
        {
            // No dejar un proyecto a medias: se puede volver a crear con los mismos datos.
            try { Directory.Delete(dir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* ya hay errores que reportar */ }
            throw;
        }
    }

    private static PluginTemplate FindTemplate(string id) =>
        PluginTemplates.All.FirstOrDefault(t => t.Id == id)
        ?? throw new SdkStepException($"Plantilla desconocida: «{id}».",
            $"Válidas: {string.Join(", ", PluginTemplates.All.Select(t => t.Id))}.");

    private static string DescriptionOrDefault(ScaffoldRequest request, PluginTemplate template) =>
        string.IsNullOrWhiteSpace(request.Description) ? template.Description : request.Description;

    private static void ValidateRequest(ScaffoldRequest request)
    {
        // Validación temprana con avisos accionables; los detalles finos los hace PluginManifest.Validate.
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new SdkStepException("Falta el id del plugin.",
                "Ejemplo: --id com.empresa.miplugin (minúsculas con puntos).");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new SdkStepException("Falta el nombre visible del plugin.", "Ejemplo: --name \"Mi plugin\".");
        if (!PluginVersion.TryParse(request.Version, out _))
            throw new SdkStepException($"La versión «{request.Version}» no es válida.",
                "Usa MAYOR.MENOR.PARCHE, p. ej. 0.1.0.");
    }

    private static void Write(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
    }
}
