namespace Yaeger.ECS;

/// <summary>
/// Thrown when a Tiled map cannot be loaded or parsed by <see cref="TiledMapLoader"/>.
/// </summary>
public sealed class TiledMapLoadException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    public TiledMapLoadException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    public TiledMapLoadException(string message, Exception inner)
        : base(message, inner) { }
}
