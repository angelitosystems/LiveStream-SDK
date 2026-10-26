using System.Text.Json;

namespace LiveStream.Cli.Tests;

/// <summary>
/// E2E del binario real: cada prueba ejecuta `lsplugin` como lo haría el usuario, contra carpetas temporales.
/// Contrato de códigos de salida: 0 correcto, 1 error de la operación, 2 uso incorrecto.
/// </summary>
public class CliE2ETests
{
    [Fact]
    public void SinArgumentosImprimeLaAyudaYYaSaleOk()
    {
        var (code, output) = CliRunner.Run();
        Assert.Equal(0, code);
        Assert.Contains("init", output);
        Assert.Contains("publish", output);
        Assert.Contains("0 correcto", output);
    }

    [Fact]
    public void VersionMuestraElNumeroDeSDK()
    {
        var (code, output) = CliRunner.Run("--version");
        Assert.Equal(0, code);
        Assert.Contains("lsplugin 1.", output);
    }

    [Fact]
    public void OrdenDesconocidaDevuelveExitDosConSugerencia()
    {
        var (code, output) = CliRunner.Run("haz-algo");
        Assert.Equal(2, code);
        Assert.Contains("Orden desconocida", output);
    }

    [Fact]
    public void OpcionHuérfanaDevuelveExitDos()
    {
        // `pack -o` sin carpeta y sin valor: uso incorrecto, no error de operación.
        var (code, output) = CliRunner.Run("pack", "-o");
        Assert.Equal(2, code);
        Assert.Contains("Falta", output);
    }

    [Fact]
    public void InitScaffoldCompletoYValidateLoAcepta()
    {
        var folder = CliRunner.TempFolder("init");
        var (code, output) = CliRunner.Run("init", folder, "--id", "com.e2e.cli", "--name", "Plan E2E",
            "--template", "graphics", "--no-interactive");
        Assert.Equal(0, code);

        var manifest = Path.Combine(folder, "plugin.json");
        Assert.True(File.Exists(manifest));
        Assert.True(File.Exists(Path.Combine(folder, "Program.cs")));
        Assert.Contains("com.e2e.cli", File.ReadAllText(manifest));

        var (validateCode, validateOutput) = CliRunner.Run("validate", folder);
        Assert.Equal(0, validateCode);
        Assert.Contains("com.e2e.cli", validateOutput);

        // El scaffold trae las plantillas sin sustituciones pendientes.
        Assert.DoesNotContain("__NAME__", File.ReadAllText(Path.Combine(folder, "Program.cs")));
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void InitConPlantillaQueNoExisteDevuelveExitDos()
    {
        var folder = CliRunner.TempFolder("bad-template");
        var (code, output) = CliRunner.Run("init", folder, "--id", "com.e2e.x", "--name", "X",
            "--template", "pog", "--no-interactive");
        Assert.Equal(2, code);
        Assert.Contains("Válidas", output);
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public void DoctorJsonDevuelveSeisComprobacionesParseables()
    {
        var (code, output) = CliRunner.Run("doctor", "--json");
        Assert.True(code is 0 or 1, $"doctor no debe quedar colgado; acabó con {code}");
        var checks = CliRunner.ParseJson<List<DoctorRow>>(output);
        Assert.NotNull(checks);
        Assert.Equal(6, checks!.Count(arg => !string.IsNullOrWhiteSpace(arg.Name)));
        Assert.All(checks, c => Assert.True(c.Name is not null));
    }

    [Fact]
    public void InitTemplatesElCatalogoVieneEnJson()
    {
        var (code, output) = CliRunner.Run("init-templates", "--json");
        Assert.Equal(0, code);
        var templates = CliRunner.ParseJson<List<TemplateRow>>(output);
        Assert.NotNull(templates);
        Assert.Equal(6, templates!.Count);
        Assert.Contains(templates, t => t.Id == "graphics");
    }

    [Fact]
    public void ListaLocalProduceSalidaFilasSinPlugins()
    {
        var (code, output) = CliRunner.Run("list");
        Assert.Equal(0, code);
        Assert.Contains("(ninguno", output);
    }

    [Fact]
    public void InfoDeUnIdQueNoExisteDevuelveErrorUno()
    {
        var (code, output) = CliRunner.Run("info", "com.faltante.noexistente");
        Assert.Equal(1, code);
        Assert.Contains("no está instalado", output);
        Assert.Contains("Sugerencia", output);
    }

    [Fact]
    public void ConfigMuestraSuUbicacion()
    {
        var (code, output) = CliRunner.Run("config");
        Assert.Equal(0, code);
        Assert.Contains("lsplugin.json", output);
    }
}

/// <summary>Fila del JSON de `doctor --json` (camelCase por el serializador del CLI).</summary>
public sealed record DoctorRow(string? Name, bool Ok, string? Detail, string? Hint, string? Section);

/// <summary>Fila del JSON de `init-templates --json`.</summary>
public sealed record TemplateRow(string? Id, string? Name, string? Description, string? Category, List<string>? Permissions);
