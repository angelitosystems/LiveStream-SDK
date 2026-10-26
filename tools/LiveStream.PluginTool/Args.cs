using LiveStream.SdkCore;

namespace LiveStream.SdkTool;

/// <summary>Lectura uniforme de la línea de órdenes: posicionales, opciones con valor y banderas globales.</summary>
public static class Args
{
    public const string ServiceUsage = "Ejemplo: lsplugin <orden> --server https://<marketplace> (el token va en la variable LS_MARKETPLACE_TOKEN).";

    public static (Ui ui, string[] rest) ParseGlobal(string[] args)
    {
        bool json = false, quiet = false, verbose = false, noInteractive = false;
        var rest = new List<string>();
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json": json = true; break;
                case "--quiet" or "-q": quiet = true; break;
                case "--verbose" or "-v": verbose = true; break;
                case "--no-interactive" or "-n": noInteractive = true; break;
                case "--color" or "--no-color": /* la presentación decide por NO_COLOR/redirigida */ break;
                default: rest.Add(arg); break;
            }
        }
        return (new Ui(json, quiet, verbose, noInteractive), rest.ToArray());
    }

    /// <summary>Argumento posicional indexado (ignorando opciones y sus valores); faltan → error de uso.</summary>
    public static string Positional(string[] rest, int index, string usage)
    {
        var values = Values(rest);
        if (index >= values.Count)
            throw new UsageException("Faltan argumentos.", usage);
        return values[index];
    }

    public static IReadOnlyList<string> PositionalList(string[] rest)
    {
        var values = Values(rest);
        if (values.Count == 0)
            throw new UsageException("Faltan argumentos.", "Revisa los ejemplos con: lsplugin help");
        return values;
    }

    public static bool Has(string[] rest, params string[] flags) =>
        rest.Any(arg => flags.Contains(arg));

    /// <summary>Valor de la opción o su valor por omisión (nulo = obligatoria).</summary>
    public static string? Option(string[] rest, string name, string? usage = null, string? fallback = null)
    {
        var index = Array.IndexOf(rest, name);
        if (index < 0)
            return fallback;
        if (index + 1 >= rest.Length || rest[index + 1].StartsWith('-'))
            throw new UsageException($"Falta el valor de {name}.", usage ?? "Revisa los ejemplos con: lsplugin help");
        return rest[index + 1];
    }

    public static bool TryFlag(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

    /// <summary>Servidor del marketplace: la opción --server o el guardado por `lsplugin config --server`.</summary>
    public static string ServerOr(string[] rest, Ui ui)
    {
        var fromOption = Option(rest, "--server");
        if (fromOption is not null)
            return fromOption;
        var saved = SdkConfigStore.Load().Server;
        if (saved is null)
            throw new UsageException("Falta el servidor del marketplace.", ServiceUsage
                + " Puedes guardarlo para siempre: lsplugin config --server https://…");
        return saved;
    }

    /// <summary>Quita opciones y valores; sirve para leer los argumentos posicionales.</summary>
    private static readonly string[] ValueOptions =
    [
        "--id", "--name", "--template", "--version", "--min-app-version", "--author", "--description", "--category", "--project",
        "-o", "--key", "--public", "--server", "--public-key", "--private",
    ];

    private static List<string> Values(IEnumerable<string> rest)
    {
        var output = new List<string>();
        var expectValue = false;
        foreach (var arg in rest)
        {
            if (expectValue)
            {
                expectValue = false;
                continue;
            }
            if (ValueOptions.Contains(arg))
                expectValue = true;
            else if (!arg.StartsWith('-'))
                output.Add(arg);
        }
        return output;
    }
}
