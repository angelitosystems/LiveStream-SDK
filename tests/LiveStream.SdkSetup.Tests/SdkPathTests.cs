using LiveStream.SdkSetup;

namespace LiveStream.SdkSetup.Tests;

public class SdkPathTests
{
    [Fact]
    public void AgregarAlPathSinDuplicados() =>
        Assert.Equal(@"C:\uno;C:\dos", SdkPath.Add(@"C:\uno;C:\dos\", @"C:\uno"));

    [Fact]
    public void ReinstalarSeleccionaAlPrincipio() =>
        Assert.Equal(@"C:\sdk;C:\uno;C:\dos", SdkPath.Add(@"C:\uno;C:\dos", @"C:\sdk"));

    [Fact]
    public void NoCambiaSiYaEstaAlPrincipio() =>
        Assert.Null(SdkPath.Add(@"C:\sdk;C:\uno", @"C:\sdk"));

    [Theory]
    [InlineData(@"C:\sdk;C:\uno;C:\dos", @"C:\uno;C:\dos")]
    [InlineData(@"C:\sdk", "")]
    [InlineData(@"C:\sdk\", "")]
    public void QuitarDelPath(string current, string expected) =>
        Assert.Equal(expected, SdkPath.Remove(current, @"C:\sdk"));

    [Fact]
    public void SinCambiosDevuelveNull() =>
        Assert.Null(SdkPath.Remove(@"C:\uno;C:\dos", @"C:\sdk"));

    [Fact]
    public void NuloNoSeToca() =>
        Assert.Null(SdkPath.Remove(null, @"C:\sdk"));
}
