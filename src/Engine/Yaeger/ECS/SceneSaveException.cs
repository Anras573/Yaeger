namespace Yaeger.ECS;

/// <summary>
/// Thrown when a world cannot be serialized or written to a scene file.
/// </summary>
public sealed class SceneSaveException : Exception
{
    public SceneSaveException(string message)
        : base(message) { }

    public SceneSaveException(string message, Exception inner)
        : base(message, inner) { }
}
