namespace Yaeger.ECS;

/// <summary>
/// Thrown when a prefab cannot be loaded or parsed.
/// </summary>
public sealed class PrefabLoadException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    public PrefabLoadException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public PrefabLoadException(string message, Exception inner)
        : base(message, inner) { }
}
