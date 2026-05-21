namespace Yaeger.Graphics;

/// <summary>
/// Platform-neutral font handle used by core gameplay data.
/// Native renderers can resolve this handle to runtime font objects.
/// </summary>
public interface IFontHandle
{
    string Id { get; }
}
