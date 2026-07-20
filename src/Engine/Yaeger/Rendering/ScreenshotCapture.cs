using Silk.NET.OpenGL;
using SkiaSharp;
using Yaeger.Windowing;

namespace Yaeger.Rendering;

/// <summary>
/// Captures a <see cref="Window"/>'s default framebuffer to a PNG file — useful for
/// screenshotting rendering bugs (e.g. from a headless/CI run) or building visual
/// regression snapshots without a human watching the live window.
/// </summary>
public static class ScreenshotCapture
{
    /// <summary>
    /// Reads back the pixels currently in <paramref name="window"/>'s framebuffer and writes
    /// them to <paramref name="path"/> as a PNG. Call this from (or after) an
    /// <see cref="Window.OnRender"/> handler, once the frame's own drawing has happened, so the
    /// framebuffer holds what was actually just rendered.
    /// </summary>
    public static void SaveFramebufferPng(Window window, string path)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var size = window.Size;
        var width = (int)size.X;
        var height = (int)size.Y;
        var gl = window.Gl;

        var pixels = new byte[width * height * 4];

        // The default PACK_ALIGNMENT of 4 assumes rows are padded to 4-byte boundaries, which
        // corrupts the readback whenever a RGBA row (width * 4 bytes) isn't itself a multiple
        // of 4 — mirrors the UNPACK_ALIGNMENT fix in FontTexture.SetData.
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        unsafe
        {
            fixed (byte* d = pixels)
            {
                gl.ReadPixels(
                    0,
                    0,
                    (uint)width,
                    (uint)height,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    d
                );
            }
        }
        gl.PixelStore(PixelStoreParameter.PackAlignment, 4);

        // glReadPixels returns rows bottom-to-top (OpenGL's origin is bottom-left); flip to the
        // top-to-bottom row order conventional image formats expect.
        var stride = width * 4;
        var flipped = new byte[pixels.Length];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(pixels, (height - 1 - y) * stride, flipped, y * stride, stride);
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var image = SKImage.FromPixelCopy(info, flipped);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
