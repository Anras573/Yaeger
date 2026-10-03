namespace Yaeger.Browser;

/// <summary>A texture failed to load in the browser (missing file, decode error, ...).</summary>
public sealed class TextureLoadException(string path, string message, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>The texture path that failed.</summary>
    public string Path { get; } = path;
}
