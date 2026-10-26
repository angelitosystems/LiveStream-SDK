using System.Text.Json;

namespace LiveStream.SdkCore;

/// <summary>Configuración persistente de `lsplugin` (la GUI usa la misma). Formato versionado.</summary>
/// <param name="Schema">Formato del archivo; hoy <c>1</c>.</param>
/// <param name="Server">Servidor del marketplace por omisión (se puede sobreescribir con --server).</param>
public sealed record SdkConfig(int Schema = 1, string? Server = null)
{
    public const int CurrentSchema = 1;

    /// <summary>Ajusta el campo y devuelve una copia con el esquema vigente.</summary>
    public SdkConfig WithServer(string? server) => this with { Schema = CurrentSchema, Server = Normalize(server) };

    private static string? Normalize(string? server) => string.IsNullOrWhiteSpace(server) ? null : server.TrimEnd('/');
}

public static class SdkConfigStore
{
    public static string Location =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveStream", "lsplugin.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Carga la configuración o una por defecto; un archivo roto se ignora (no bloquea ninguna orden).</summary>
    public static SdkConfig Load()
    {
        try
        {
            if (File.Exists(Location)
                && JsonSerializer.Deserialize<SdkConfig>(File.ReadAllText(Location)) is { Schema: >= 1 } config)
                return config with { Schema = config.Schema };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Sin config, todo sigue funcionando con valores por defecto.
        }
        return new SdkConfig();
    }

    public static void Save(SdkConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        File.WriteAllText(Location, JsonSerializer.Serialize(config, Json));
    }
}
