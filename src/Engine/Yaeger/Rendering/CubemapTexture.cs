using Silk.NET.OpenGL;
using StbImageSharp;

namespace Yaeger.Rendering;

/// <summary>
/// Loads six images into an OpenGL cubemap texture.
/// Face order: right (+X), left (−X), top (+Y), bottom (−Y), front (+Z), back (−Z).
/// </summary>
public sealed class CubemapTexture : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    public unsafe CubemapTexture(
        GL gl,
        string right,
        string left,
        string top,
        string bottom,
        string front,
        string back
    )
    {
        _gl = gl;
        _handle = _gl.GenTexture();
        var success = false;
        try
        {
            _gl.BindTexture(TextureTarget.TextureCubeMap, _handle);

            // Cubemap faces must not be flipped; restore after loading.
            StbImage.stbi_set_flip_vertically_on_load(0);
            try
            {
                TextureTarget[] faceTargets =
                [
                    TextureTarget.TextureCubeMapPositiveX,
                    TextureTarget.TextureCubeMapNegativeX,
                    TextureTarget.TextureCubeMapPositiveY,
                    TextureTarget.TextureCubeMapNegativeY,
                    TextureTarget.TextureCubeMapPositiveZ,
                    TextureTarget.TextureCubeMapNegativeZ,
                ];
                string[] paths = [right, left, top, bottom, front, back];

                int faceWidth = 0,
                    faceHeight = 0;
                for (var i = 0; i < 6; i++)
                {
                    using var stream = File.OpenRead(AssetPath.Resolve(paths[i]));
                    var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                    if (image.Width != image.Height)
                        throw new ArgumentException(
                            $"Cubemap face '{paths[i]}' is not square ({image.Width}x{image.Height})."
                        );

                    if (i == 0)
                    {
                        faceWidth = image.Width;
                        faceHeight = image.Height;
                    }
                    else if (image.Width != faceWidth || image.Height != faceHeight)
                    {
                        throw new ArgumentException(
                            $"Cubemap face '{paths[i]}' size {image.Width}x{image.Height} does not match "
                                + $"face 0 size {faceWidth}x{faceHeight}."
                        );
                    }

                    fixed (byte* data = image.Data)
                    {
                        _gl.TexImage2D(
                            faceTargets[i],
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
                }
            }
            finally
            {
                StbImage.stbi_set_flip_vertically_on_load(1);
            }

            _gl.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureMinFilter,
                (int)GLEnum.Linear
            );
            _gl.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureMagFilter,
                (int)GLEnum.Linear
            );
            _gl.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureWrapS,
                (int)GLEnum.ClampToEdge
            );
            _gl.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureWrapT,
                (int)GLEnum.ClampToEdge
            );
            _gl.TexParameter(
                TextureTarget.TextureCubeMap,
                TextureParameterName.TextureWrapR,
                (int)GLEnum.ClampToEdge
            );

            _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
            success = true;
        }
        finally
        {
            if (!success)
            {
                _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
                _gl.DeleteTexture(_handle);
            }
        }
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.TextureCubeMap, _handle);
    }

    public void Unbind(TextureUnit unit = TextureUnit.Texture0)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}
