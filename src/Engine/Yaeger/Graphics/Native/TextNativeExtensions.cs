namespace Yaeger.Graphics;

/// <summary>Native-only helpers for <see cref="Text"/>, which itself lives in Yaeger.Core.</summary>
public static class TextNativeExtensions
{
    /// <summary>
    /// Returns the native font instance when the component was created from one.
    /// </summary>
    public static bool TryGetNativeFont(this Text text, out Font.Font nativeFont)
    {
        if (text.FontObject is Font.Font font)
        {
            nativeFont = font;
            return true;
        }

        nativeFont = default!;
        return false;
    }
}
