using System.Text;
using System.Text.Json;
using LiveStream.PluginSdk;
using LiveStream.SdkCore;

namespace LiveStream.SdkTool;

/// <summary>
/// Capa de presentación del CLI: colores (respetando NO_COLOR y la salida redirigida), niveles --quiet/--verbose,
/// salida --json y confirmaciones para acciones peligrosas. Toda la lógica vive en LiveStream.SdkCore.
/// </summary>
public sealed class Ui
{
    private const string OkColor = "\u001b[32m";
    private const string WarnColor = "\u001b[33m";
    private const string ErrorColor = "\u001b[31m";
    private const string MutedColor = "\u001b[90m";
    private const string Bold = "\u001b[1m";
    private const string Reset = "\u001b[0m";

    public Ui(bool json, bool quiet, bool verbose, bool noInteractive)
    {
        Json = json;
        Quiet = quiet;
        Verbose = verbose;
        Interactive = !noInteractive && !json && !Console.IsInputRedirected;
        Color = !json
            && Environment.GetEnvironmentVariable("NO_COLOR") is null
            && !Console.IsOutputRedirected;
    }

    public bool Json { get; }
    public bool Quiet { get; }
    public bool Verbose { get; }
    public bool Interactive { get; }
    public bool Color { get; }

    /// <summary>Línea informativa (con --quiet o --json no se ve: los datos van por JSON).</summary>
    public void Line(string text)
    {
        if (!Json && !Quiet)
            Console.Out.WriteLine(text);
    }

    /// <summary>Detalle técnico adicional, solo con --verbose.</summary>
    public void Detail(string text)
    {
        if (!Json && Verbose)
            Console.Out.WriteLine(Mut(MutedColor, "   " + text));
    }

    public void Ok(string text) => Status(OkColor, text);

    public void Warn(string text) => Status(WarnColor, text);

    public void Error(string text, string? hint)
    {
        if (Json)
            return; // en --json el error lo emite Main en formato JSON.
        Console.Error.WriteLine(Mut(ErrorColor, "Error: " + text));
        if (!string.IsNullOrWhiteSpace(hint))
            Console.Error.WriteLine(Mut(MutedColor, "  Sugerencia: " + hint));
    }

    public void ErrorPlain(string text, string? hint) => Error(text, hint);

    /// <summary>Salida --json de datos estructurados.</summary>
    public void JsonOut<T>(T value) where T : notnull
    {
        if (!Json)
            return;
        Console.Out.WriteLine(Serialize(value));
    }

    public static string Serialize<T>(T value) where T : notnull
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        return JsonSerializer.Serialize(value, options);
    }

    /// <summary>Pregunta sí/no. Debe activarse --yes en JSON o CI (--no-interactive).</summary>
    public bool Confirm(string question, bool assumeDefault)
    {
        if (!Interactive)
        {
            if (assumeDefault)
                return true;
            throw new SdkStepException("Esta acción necesita confirmación.",
                "Ejecútala con --yes si la quieres aplicar, o en una terminal sin --no-interactive para responder aquí.");
        }
        Console.Out.Write(Mut(WarnColor, question + " [s/N]? "));
        Console.Out.Flush();
        var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return answer is "s" or "si" or "sí" or "y" or "yes" or "1";
    }

    /// <summary>Pregunta de texto con valor por omisión (solo modo interactivo).</summary>
    public string Ask(string question, string? defaultValue)
    {
        if (!Interactive)
            throw new SdkStepException($"Falta el dato «{question}».",
                "Dale el valor con la opción correspondiente (p. ej. --id) o ejecuta en una terminal sin --no-interactive.");
        var suffix = string.IsNullOrEmpty(defaultValue) ? ": " : $" [{defaultValue}]: ";
        Console.Out.Write(Emphasize(question) + suffix);
        Console.Out.Flush();
        var answer = Console.ReadLine()?.Trim() ?? string.Empty;
        return answer.Length > 0 ? answer : defaultValue ?? string.Empty;
    }

    private string Emphasize(string text) => Mut(Bold, text);

    private void Status(string color, string text)
    {
        if (!Json && !Quiet)
            Console.Out.WriteLine(Mut(color, text));
    }

    private string Mut(string color, string text) => Color ? color + text + Reset : text;
}

/// <summary>Uso incorrecto de la línea de órdenes: Main lo trata como código de salida 2.</summary>
public sealed class UsageException(string message, string? hint = null) : Exception(message)
{
    public string? Hint { get; } = hint;
}
