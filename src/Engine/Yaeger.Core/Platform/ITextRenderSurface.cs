using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Platform;

/// <summary>
/// Rendering abstraction for text drawing.
/// </summary>
public interface ITextRenderSurface
{
    void DrawText(string text, Matrix4x4 transform, IFontHandle font, int fontSize, Color color);
}
