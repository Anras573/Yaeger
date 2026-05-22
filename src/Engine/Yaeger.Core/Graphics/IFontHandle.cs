namespace Yaeger.Graphics;

/// <summary>
/// Platform-neutral font handle used by core gameplay data.
/// Native renderers can resolve this handle to runtime font objects.
/// </summary>
public interface IFontHandle
{
    /// <summary>
    /// A non-empty font identifier that native renderers can resolve to a concrete font.
    /// </summary>
    /// <remarks>
    /// In the default native adapter this is passed to <c>FontManager.Load(...)</c>, so it
    /// should be a resolvable font asset key/path.
    /// </remarks>
    string Id { get; }
}
