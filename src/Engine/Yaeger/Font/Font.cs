using HarfBuzzSharp;

namespace Yaeger.Font;

public class Font : IDisposable
{
    private readonly Blob _blob;
    private readonly Face _face;
    private readonly HarfBuzzSharp.Font _font;
    private bool _disposed;

    public Font(string fontPath)
    {
        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"Font file not found: {fontPath}");
        }

        var fontBytes = File.ReadAllBytes(fontPath);
        _blob = Blob.FromStream(new MemoryStream(fontBytes));
        _face = new Face(_blob, 0);
        _font = new HarfBuzzSharp.Font(_face);
    }

    public HarfBuzzSharp.Font HarfBuzzFont => _font;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _font?.Dispose();
        _face?.Dispose();
        _blob?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}