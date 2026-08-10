using Silk.NET.OpenGL;
using StbImageSharp;

namespace Yaeger.Rendering;

public class Texture : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly string _path;

    public int Width { get; private set; }
    public int Height { get; private set; }

    // Flip images vertically on load so that OpenGL's bottom-up texture
    // coordinate convention (v=0 at the bottom) matches the uploaded data.
    static Texture() => StbImage.stbi_set_flip_vertically_on_load(1);

    public unsafe Texture(GL gl, string path)
    {
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

        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Linear
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)GLEnum.Repeat
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.Repeat
        );
        _gl.GenerateMipmap(TextureTarget.Texture2D);
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
