using Silk.NET.OpenGL;
using StbImageSharp;
using Yaeger.Platform;

namespace Yaeger.Rendering;

public class Texture : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly string? _path;
    private TextureSampling _sampling;

    public int Width { get; private set; }
    public int Height { get; private set; }

    // Flip images vertically on load so that OpenGL's bottom-up texture
    // coordinate convention (v=0 at the bottom) matches the uploaded data.
    static Texture() => StbImage.stbi_set_flip_vertically_on_load(1);

    public unsafe Texture(GL gl, string path, TextureSampling sampling = default)
    {
        if (sampling == default)
            sampling = TextureSampling.Default;
        _gl = gl;
        _path = path;
        _handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _handle);

        using var stream = File.OpenRead(AssetPath.Resolve(path));
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        Width = image.Width;
        Height = image.Height;

        fixed (byte* data = image.Data)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                (uint)image.Width,
                (uint)image.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data
            );
        }

        _sampling = sampling;
        ApplySampling();
        if (sampling.UsesMipmaps)
            _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    /// <summary>The sampling state currently applied to this texture.</summary>
    public TextureSampling Sampling => _sampling;

    /// <summary>Changes filter/wrap in place, generating mipmaps if newly required.</summary>
    public void SetSampling(TextureSampling sampling)
    {
        if (sampling == _sampling || _path is null)
            return;
        _sampling = sampling;
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
        ApplySampling();
        if (sampling.UsesMipmaps)
            _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    private void ApplySampling()
    {
        var min = _sampling.Filter switch
        {
            TextureFilter.Nearest => GLEnum.Nearest,
            TextureFilter.LinearMipmap => GLEnum.LinearMipmapLinear,
            _ => GLEnum.Linear,
        };
        var mag = _sampling.Filter == TextureFilter.Nearest ? GLEnum.Nearest : GLEnum.Linear;
        var wrap = _sampling.Wrap == TextureWrap.Repeat ? GLEnum.Repeat : GLEnum.ClampToEdge;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
    }

    /// <summary>Creates a 1×1 opaque white texture, used to draw flat-coloured (untextured) quads.</summary>
    public unsafe Texture(GL gl)
    {
        _gl = gl;
        _handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _handle);

        Width = 1;
        Height = 1;

        byte* white = stackalloc byte[4] { 255, 255, 255, 255 };
        _gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            (int)InternalFormat.Rgba,
            1,
            1,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            white
        );

        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Nearest
        );
    }

    /// <summary>
    /// Re-reads the file at this texture's path and re-uploads it into the existing GL handle,
    /// so every draw call already holding this <see cref="Texture"/> instance picks up the new
    /// pixels with no handle churn. The image is fully decoded before any GL call is made, so a
    /// truncated or malformed file (e.g. mid-save) leaves the currently-bound texture untouched
    /// and this method returns <see langword="false"/> instead of throwing.
    /// </summary>
    public unsafe bool TryReload()
    {
        if (_path is null)
            return false;

        ImageResult image;
        try
        {
            using var stream = File.OpenRead(AssetPath.Resolve(_path));
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }
        catch (Exception ex)
        {
            // The file is read from disk and decoded by a third-party image library, so a
            // mid-write or corrupt file can fail in ways we don't control (I/O errors, decode
            // failures). Nothing has touched the GL texture yet, so the currently-bound image
            // is left exactly as it was.
            Console.Error.WriteLine(
                $"[AssetWatcher] Failed to reload texture '{_path}': {ex.Message}"
            );
            return false;
        }

        _gl.BindTexture(TextureTarget.Texture2D, _handle);

        fixed (byte* data = image.Data)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                (uint)image.Width,
                (uint)image.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                data
            );
        }

        if (_sampling.UsesMipmaps)
            _gl.GenerateMipmap(TextureTarget.Texture2D);

        Width = image.Width;
        Height = image.Height;

        return true;
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
    }

    public void Unbind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}
