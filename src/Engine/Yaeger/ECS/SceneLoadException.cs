namespace Yaeger.ECS;

/// <summary>
/// Thrown when a scene cannot be loaded or parsed.
/// </summary>
public sealed class SceneLoadException : Exception
{
    public SceneLoadException(string message)
        : base(message) { }

    public SceneLoadException(string message, Exception inner)
        : base(message, inner) { }
}
