using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class FontTexture : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    public unsafe FontTexture(GL gl, int width, int height)
    {
        _gl = gl;
        _handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _handle);

        var emptyData = new byte[width * height];
        fixed (byte* data = emptyData)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.R8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Red,
                PixelType.UnsignedByte,
                data
            );
        }

        // Use LINEAR filtering for smooth text at various sizes
        // Don't use mipmaps for font atlas - we update regions dynamically
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
            (int)GLEnum.ClampToEdge
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)GLEnum.ClampToEdge
        );

        _gl.BindTexture(TextureTarget.Texture2D, 0);
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

    public void SetData<T>(
        ReadOnlySpan<T> data,
        int xOffset,
        int yOffset,
        int width,
        int height,
        int sourceRowLengthInPixels = 0
    )
        where T : unmanaged
    {
        Bind();

        // R8 uploads are byte-aligned; the default GL_UNPACK_ALIGNMENT of 4 assumes rows
        // are padded to 4-byte boundaries, which silently corrupts glyph uploads whenever
        // `width % 4 != 0`. If the source's row stride differs from `width`, also set
        // UNPACK_ROW_LENGTH so GL reads rows at the correct offset.
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        var needsRowLength = sourceRowLengthInPixels != 0 && sourceRowLengthInPixels != width;
        if (needsRowLength)
        {
            _gl.PixelStore(PixelStoreParameter.UnpackRowLength, sourceRowLengthInPixels);
        }

        unsafe
        {
            fixed (void* d = data)
            {
                _gl.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0,
                    xOffset,
                    yOffset,
                    (uint)width,
                    (uint)height,
                    PixelFormat.Red,
                    PixelType.UnsignedByte,
                    d
                );
            }
        }

        // Restore GL defaults so non-font texture uploads elsewhere aren't affected.
        if (needsRowLength)
        {
            _gl.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        }
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

        Unbind();
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}
