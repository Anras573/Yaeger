namespace Yaeger.Font;

public class FontManager : IDisposable
{
    private readonly Dictionary<string, Font> _fonts = new();
    private bool _disposed;

    public Font Load(string fontPath)
    {
        var resolvedPath = AssetPath.Resolve(fontPath);
        if (_fonts.TryGetValue(resolvedPath, out var existingFont))
        {
            return existingFont;
        }

        var font = new Font(resolvedPath);
        _fonts[resolvedPath] = font;
        return font;
    }

    public Font? Get(string fontPath)
    {
        var resolvedPath = AssetPath.Resolve(fontPath);
        return _fonts.TryGetValue(resolvedPath, out var font) ? font : null;
    }

    public void Unload(string fontPath)
    {
        var resolvedPath = AssetPath.Resolve(fontPath);
        if (_fonts.TryGetValue(resolvedPath, out var font))
        {
            font.Dispose();
            _fonts.Remove(resolvedPath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _fonts.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
