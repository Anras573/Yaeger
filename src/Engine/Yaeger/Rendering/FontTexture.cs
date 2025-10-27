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
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.R8,
                (uint)width, (uint)height, 0,
                PixelFormat.Red, PixelType.UnsignedByte, data);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.GenerateMipmap(TextureTarget.Texture2D);

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

    public void SetData<T>(ReadOnlySpan<T> data, int xOffset, int yOffset, int width, int height) where T : unmanaged
    {
        Bind();
        unsafe
        {
            fixed (void* d = data)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, xOffset, yOffset, (uint)width, (uint)height, PixelFormat.Red, PixelType.UnsignedByte, d);
            }
        }
        Unbind();
    }

    public void SetData(Span<Byte> data, int xOffset, int yOffset, int width, int height)
    {
        Bind();
        unsafe
        {
            fixed (void* d = data)
            {
                _gl.TexSubImage2D(TextureTarget.Texture2D, 0, xOffset, yOffset, (uint)width, (uint)height, PixelFormat.Red, PixelType.UnsignedByte, d);
            }
        }
        Unbind();
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}