using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkCore.Tests;

/// <summary>
/// Ciclo completo del núcleo en carpetas temporales: empaquetar → firmar → verificar, y el almacén de plugins
/// (los mismos formatos `plugins.json` y `.lsplugin` de la app) con instalación/activación/desinstalación.
/// Nunca toca el almacén real del usuario: PluginStore con installRoot/dataRoot temporales.
/// </summary>
public class PackagingAndStoreTests
{
    [Fact]
    public void PackEsReproducibleElMismoContenidoDaElMismoSha()
    {
        using var workspace = Workspace();
        var dir = MakePlugin(workspace.Solve("basic"));
        var first = PackagingService.Pack(dir, workspace.Solve("a.lsplugin"));
        var second = PackagingService.Pack(dir, workspace.Solve("b.lsplugin"));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.Sha256, PluginPackage.Sha256(second.Path));
    }

    [Fact]
    public void FirmarYVerificarElCicloCompleto()
    {
        using var workspace = Workspace();
        var package = PackagingService.Pack(MakePlugin(workspace.Solve("basic")), workspace.Solve("p.lsplugin")).Path;

        // keygen/sign/verify sobre %TEMP% (nunca dentro de un repositorio git ni sobre claves del usuario).
        var pair = SigningService.Keygen(workspace.Solve("private.pem"), workspace.Solve("plugin-public-key.pem"));
        Assert.True(File.Exists(pair.PrivatePath));
        Assert.True(File.Exists(pair.PublicPath));

        var signature = SigningService.Sign(package, pair.PrivatePath);
        Assert.Equal(package + PackageSignature.FileSuffix, workspace.Solve("p.lsplugin.sig.json"));
        Assert.Equal(PluginPackage.Inspect(package).Id, signature.Id);

        var verified = SigningService.Verify(package, pair.PublicPath);
        Assert.Equal(signature.Sha256, verified.Sha256);

        // Un paquete distinto no valida con esta firma (los bytes del paquete entran en la huella).
        var other = PackagingService.Pack(MakePlugin(workspace.Solve("otro")), workspace.Solve("otro.lsplugin")).Path;
        Assert.Throws<SdkStepException>(() => SigningService.Verify(other, pair.PublicPath));
    }

    [Fact]
    public void VerificarConClaveAjenaFalla()
    {
        using var workspace = Workspace();
        var package = PackagingService.Pack(MakePlugin(workspace.Solve("basic")), workspace.Solve("p1.lsplugin")).Path;
        var publicador = SigningService.Keygen(workspace.Solve("a-private.pem"), workspace.Solve("a-public.pem"));
        var intruso = SigningService.Keygen(workspace.Solve("b-private.pem"), workspace.Solve("b-public.pem"));
        SigningService.Sign(package, publicador.PrivatePath);
        // La clave de «intruso» no puede verificar la firma del publicador.
        Assert.Throws<SdkStepException>(() => SigningService.Verify(package, intruso.PublicPath));
    }

    [Fact]
    public void InstalarEnableDisableUninstall()
    {
        using var workspace = Workspace();
        var store = NewStore(workspace);
        var package = PackagingService.Pack(MakePlugin(workspace.Solve("basic")), workspace.Solve("p.lsplugin")).Path;

        // Sin firma: exige DeveloperMode (modificada en la instancia temporal, no la global).
        Assert.Throws<PluginValidationException>(() =>
            new PluginStore(new Version(0, 9, 0), null, store.InstallRoot, store.DataRoot).Install(package, null, "test", enable: true));

        var installed = store.Install(package, signature: null, source: "test", enable: true);
        Assert.False(installed.Record.Signed);
        var found = store.Find(installed.Id);
        Assert.NotNull(found);
        Assert.Equal(installed.Manifest.Version, found!.Record.Version);

        store.SetEnabled(installed.Id, false);
        Assert.False(store.Find(installed.Id)!.Record.Enabled);
        store.SetEnabled(installed.Id, true);
        Assert.True(store.Find(installed.Id)!.Record.Enabled);

        store.Uninstall(installed.Id);
        Assert.Empty(store.List());
        Assert.False(Directory.Exists(Path.Combine(store.InstallRoot, installed.Id)));
    }

    [Fact]
    public void InstalarFirmadoConClaveDeConfianza()
    {
        using var workspace = Workspace();
        var package = PackagingService.Pack(MakePlugin(workspace.Solve("basic")), workspace.Solve("p.lsplugin")).Path;
        var pair = SigningService.Keygen(workspace.Solve("private.pem"), workspace.Solve("public.pem"));
        SigningService.Sign(package, pair.PrivatePath);

        // Almacén con la clave pública compilada (el camino que usa la app real):
        var store = new PluginStore(new Version(0, 9, 0), File.ReadAllText(pair.PublicPath),
            workspace.Solve("plugins"), workspace.Solve("datos"));
        var installed = store.Install(package, SigningService.LoadSideward(package), source: "test", enable: true);
        Assert.True(installed.Record.Signed);
    }

    [Fact]
    public void IncompatibleConLaAppSeNiega()
    {
        using var workspace = Workspace();
        var store = NewStore(workspace, appVersion: new Version(0, 8, 1));
        var package = PackagingService.Pack(MakePlugin(workspace.Solve("basic")), workspace.Solve("p.lsplugin")).Path;
        var exception = Assert.Throws<PluginValidationException>(() =>
            store.Install(package, null, "test", enable: true));
        Assert.Contains("0.9.0", exception.Message);
    }

    [Fact]
    public void DesinstalarFallaConDependientes()
    {
        using var workspace = Workspace();
        var store = NewStore(workspace);
        var basePackage = PackagingService.Pack(MakePlugin(workspace.Solve("base")), workspace.Solve("base.lsplugin")).Path;
        var baseInstalled = store.Install(basePackage, null, "test", enable: true);

        // Un segundo plugin que depende del base: mismo scaffold con identidad y dependencias propias.
        var dir = MakePlugin(workspace.Solve("dependiente"));
        var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(dir, PluginManifest.FileName)));
        File.WriteAllText(Path.Combine(dir, PluginManifest.FileName),
            System.Text.Json.JsonSerializer.Serialize(manifest with
            {
                Id = "com.test.dependiente",
                Name = "Plugin dependiente",
                Dependencies = new Dictionary<string, string> { [baseInstalled.Id] = "0.1.0" },
            }, PluginManifest.Json));
        var dependientePackage = PackagingService.Pack(dir, workspace.Solve("depe.lsplugin")).Path;
        store.Install(dependientePackage, null, "test", enable: true);

        // El almacén se niega a desinstalar el base mientras hay un dependiente…
        var exception = Assert.Throws<PluginValidationException>(() => store.Uninstall(baseInstalled.Id));
        Assert.Contains("lo necesitan", exception.Message.ToLowerInvariant());
        // …y orden inverso sí funciona.
        store.Uninstall("com.test.dependiente");
        store.Uninstall(baseInstalled.Id);
        Assert.Empty(store.List());
    }

    /// <summary>Doctor: seis comprobaciones con nombre, nunca lanza (un equipo de CI también debe pasarla).</summary>
    [Fact]
    public async Task DoctorNuncaLanzaDaSeisChequeos()
    {
        var checks = await new DoctorService().CheckAsync();
        Assert.Equal(6, checks.Count);
        Assert.All(checks, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
    }

    [Fact]
    public void ConfigNormalizaElServidor()
    {
        Assert.Equal("https://marketplace/x", new SdkConfig().WithServer("https://marketplace/x/").Server);
        Assert.Null(new SdkConfig().WithServer("   ").Server);
    }

    /// <summary>Almacén aislado (directorios temporales) con modo desarrollador (verificar firma ya tiene su prueba).</summary>
    private static PluginStore NewStore(TempDir workspace, Version? appVersion = null, bool developerMode = true)
    {
        var store = new PluginStore(appVersion ?? new Version(0, 9, 0), trustedPublicKey: null,
            workspace.Solve("plugins"), workspace.Solve("datos"));
        store.DeveloperMode = developerMode;
        return store;
    }

    /// <summary>Proyecto de plugin listo para empaquetar: `Pack` exige que exista el entry declarado.</summary>
    private static string MakePlugin(string dir)
    {
        Directory.CreateDirectory(dir);
        var scaffold = new ScaffoldService().Create(dir, new ScaffoldRequest("basic", "com.test.store", "Almacén"));
        File.WriteAllText(Path.Combine(dir, scaffold.Manifest.Entry), "fake exe");
        return dir;
    }

    private static TempDir Workspace()
    {
        var path = System.IO.Path.Combine(Path.GetTempPath(), "lsdkcore-tests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return new TempDir(path);
    }

    private sealed class TempDir(string path) : IDisposable
    {
        public string Solve(string name) => System.IO.Path.Combine(path, name);

        public void Dispose()
        {
            try { Directory.Delete(path, recursive: true); }
            catch (IOException) { /* limpieza best effort */ }
        }
    }
}
