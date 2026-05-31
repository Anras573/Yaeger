namespace Yaeger.Font;

/// <summary>
/// Thrown when a font cannot be loaded from disk or a remote URL.
/// </summary>
public sealed class FontLoadException : Exception
{
    public FontLoadException(string message)
        : base(message) { }

    public FontLoadException(string message, Exception inner)
        : base(message, inner) { }
}
