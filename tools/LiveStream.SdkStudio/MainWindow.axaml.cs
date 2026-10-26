using Avalonia.Controls;

namespace LiveStream.SdkStudio;

/// <summary>
/// Parte designada por el XAML (x:Class). La implementación está repartida en los archivos parciales:
///   MainWindow.Core.cs    — estado compartido, Log, helpers de diálogos y filas del modelo
///   MainWindow.Home.cs    — Inicio y asistente Crear plugin
///   MainWindow.Package.cs — Manifiesto y Paquete (validar/compilar/empaquetar/firmar/verificar)
///   MainWindow.Storage.cs — Plugins instalados y Diagnóstico
/// </summary>
public partial class MainWindow : Window
{
}
