using Avalonia;
using LiveStream.SdkStudio;

// Punto de entrada de la GUI del SDK. BuildAvaloniaApp define el arranque con FluentTheme dark (App.axaml).
class Program
{
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
