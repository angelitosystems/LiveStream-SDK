namespace LiveStream.SdkCore;

/// <summary>Fallo de un paso del flujo (scaffold, build, paquete, instalación…) con una solución sugerida.</summary>
public sealed class SdkStepException(string message, string? hint = null) : Exception(message)
{
    /// <summary>Qué hacer para resolverlo, en lenguaje para quien usa la herramienta. Null si el mensaje ya lo cubre.</summary>
    public string? Hint { get; } = hint;
}
