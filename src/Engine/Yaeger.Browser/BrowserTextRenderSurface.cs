using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// No-op <see cref="ITextRenderSurface"/> for the browser host.
/// SkiaSharp and HarfBuzz are not available in the browser WASM runtime;
/// text draw calls are silently discarded.
/// </summary>
public sealed class BrowserTextRenderSurface : ITextRenderSurface
{
    public void DrawText(
        string text,
        Matrix4x4 transform,
        FontHandle font,
        int fontSize,
        Color color
    ) { }

    public void DrawText(
        string text,
        Matrix4x4 transform,
        IFontHandle font,
        int fontSize,
        Color color
    ) { }
}
