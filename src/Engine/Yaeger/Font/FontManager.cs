using System.Reflection;

namespace Yaeger.Font;

public class FontManager : IDisposable
{
    private readonly Dictionary<string, Font> _fonts = new();
    private bool _disposed;
    private readonly string _assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

    public Font Load(string fontPath)
    {
        if (_fonts.TryGetValue(fontPath, out var existingFont))
        {
            return existingFont;
        }

        var font = new Font(Path.Combine(_assemblyPath, fontPath));
        _fonts[fontPath] = font;
        return font;
    }

    public Font? Get(string fontPath)
    {
        return _fonts.TryGetValue(fontPath, out var font) ? font : null;
    }

    public void Unload(string fontPath)
    {
        if (_fonts.TryGetValue(fontPath, out var font))
        {
            font.Dispose();
            _fonts.Remove(fontPath);
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