using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

// The 1x1 fallback textures every gated sampler (PBR maps, shadow map, IBL cubemaps) binds to
// when its feature is disabled, so a statically-used sampler always points at a complete texture.
public sealed partial class Renderer3D
{
    // Bind the 1×1 white texture to the optional PBR sampler units (2-4) once at construction.
    // Those samplers are statically used by the fragment shader (the gating `uHas*Map` uniform
    // doesn't make them un-referenced), so each must point at a *complete* texture for defined
    // behaviour. Draw only ever overwrites these units with a real map and never unbinds them,
    // so this one-time bind keeps the units complete for the renderer's lifetime — Draw can then
    // skip binding a fallback when a map is absent.
    private void BindDefaultPbrTextures()
    {
        foreach (
            var unit in (ReadOnlySpan<TextureUnit>)
                [TextureUnit.Texture2, TextureUnit.Texture3, TextureUnit.Texture4]
        )
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        }

        // Restore the default active unit so we don't leak Texture4 into later GL setup (e.g. the
        // Texture constructor binds without first selecting a unit).
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    private unsafe uint CreateWhiteTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] white = [255, 255, 255, 255];
        fixed (byte* ptr = white)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                1,
                1,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest
        );
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    // Flat normal map: (0.5, 0.5, 1.0) encodes tangent-space normal pointing straight up.
    private unsafe uint CreateFlatNormalTexture()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        byte[] flatNormal = [128, 128, 255, 255];
        fixed (byte* ptr = flatNormal)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                (int)InternalFormat.Rgba,
                1,
                1,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.Nearest
        );
        _gl.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Nearest
        );
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        return handle;
    }

    // 1x1 white cubemap: a complete fallback for the IBL cubemap samplers when no EnvironmentMap
    // is bound (mirrors CreateWhiteTexture's role for the 2D PBR/shadow fallbacks).
    private unsafe uint CreateWhiteCubemap()
    {
        var handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.TextureCubeMap, handle);
        byte[] white = [255, 255, 255, 255];
        TextureTarget[] faces =
        [
            TextureTarget.TextureCubeMapPositiveX,
            TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY,
            TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ,
            TextureTarget.TextureCubeMapNegativeZ,
        ];
        fixed (byte* ptr = white)
        {
            foreach (var face in faces)
            {
                _gl.TexImage2D(
                    face,
                    0,
                    (int)InternalFormat.Rgba,
                    1,
                    1,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr
                );
            }
        }
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMinFilter,
            (int)GLEnum.Nearest
        );
        _gl.TexParameter(
            TextureTarget.TextureCubeMap,
            TextureParameterName.TextureMagFilter,
            (int)GLEnum.Nearest
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
        return handle;
    }
}
