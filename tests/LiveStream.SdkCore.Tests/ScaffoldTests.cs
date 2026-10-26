using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkCore.Tests;

/// <summary>
/// Scaffold de las 6 plantillas. Cada proyecto generado debe pasar la validación del manifiesto y no dejar
/// sustituciones pendientes; el compilado real se cubre en las pruebas de la CLI (con dotnet publish real).
/// </summary>
public class ScaffoldTests
{
    [Fact]
    public void CadaPlantillaGeneraUnProyectoValido()
    {
        foreach (var template in PluginTemplates.All)
            using (var dir = TempDirectory())
            {
                var scaffold = new ScaffoldService().Create(dir.Path, new ScaffoldRequest(
                    TemplateId: template.Id, Id: $"com.test.{template.Id}", Name: "Prueba " + template.Id));

                var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(dir.Path, PluginManifest.FileName)));
                Assert.Equal($"com.test.{template.Id}", manifest.Id);
                Assert.Equal(template.Category, manifest.Category);
                Assert.Equal(template.Permissions, manifest.PermissionList);
                Assert.Null(manifest.Validate());

                // El código de plantilla trae todas las sustituciones resueltas (__NAME__, __SDKREF__, __SDKDIR__).
                foreach (var file in Directory.EnumerateFiles(dir.Path))
                    Assert.DoesNotContain("__", File.ReadAllText(file));
            }
    }

    [Fact]
    public void DesdeElRepositorioElCsprojReferenciaElSdkPorProjectReference()
    {
        using var dir = TempDirectory();
        var scaffold = new ScaffoldService().Create(dir.Path, new ScaffoldRequest("basic", "com.test.ref", "Ref"));
        Assert.Contains("<ProjectReference", File.ReadAllText(scaffold.ProjectPath));
    }

    [Fact]
    public void RechazaFaltantesConMensajeAccionable()
    {
        using var dir = TempDirectory();
        var exception = Assert.Throws<SdkStepException>(() => new ScaffoldService().Create(dir.Path,
            new ScaffoldRequest("basic", "", "Vacío")));
        Assert.Contains("id del plugin", exception.Message);
    }

    [Fact]
    public void SeNiegaSiLaCarpetaTieneContenido()
    {
        using var dir = TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, "algo.txt"), "x");
        Assert.Throws<SdkStepException>(() => new ScaffoldService().Create(dir.Path,
            new ScaffoldRequest("basic", "com.test.x", "X")));
    }

    [Fact]
    public void PlantillaDesconocidaAvisaLasValidas()
    {
        using var dir = TempDirectory();
        var exception = Assert.Throws<SdkStepException>(() => new ScaffoldService().Create(dir.Path,
            new ScaffoldRequest("noexiste", "com.test.x", "X")));
        Assert.Contains("graphics", exception.Hint);
    }

    /// <summary>Carpeta temporal exclusiva de cada prueba; el using garantiza la limpieza.</summary>
    private static TempDir TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "lsdkcore-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    private sealed class TempDir(string path) : IDisposable
    {
        public string Path { get; } = path;

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { /* limpieza best effort */ }
        }
    }
}
