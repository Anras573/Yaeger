using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

// Image-based lighting: binding a prefiltered EnvironmentMap for the PBR path's ambient term.
public sealed partial class Renderer3D
{
    // The IBL samplers (irradiance/prefiltered cubemaps, BRDF LUT) are statically used by the
    // fragment shader, so — like the PBR and shadow fallbacks — they must point at complete
    // textures even when uUseIBL is 0. Bind the 1x1 white cubemap to units 6/7 and reuse the
    // existing 1x1 white 2D texture for unit 8 until SetEnvironmentMap swaps in real resources.
    private void BindDefaultIblTextures()
    {
        foreach (
            var unit in (ReadOnlySpan<TextureUnit>)[TextureUnit.Texture6, TextureUnit.Texture7]
        )
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.TextureCubeMap, _defaultCubemap);
        }

        _gl.ActiveTexture(TextureUnit.Texture8);
        _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);

        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>
    /// Binds a prefiltered <see cref="EnvironmentMap"/> (irradiance + prefiltered specular + BRDF
    /// LUT) and enables image-based lighting for the PBR path. Call once per frame, before the
    /// draw loop, whenever the scene has a skybox with a registered <see cref="EnvironmentMap"/>.
    /// Has no effect on the Blinn-Phong path.
    /// </summary>
    public void SetEnvironmentMap(EnvironmentMap environmentMap)
    {
        ArgumentNullException.ThrowIfNull(environmentMap);

        _shader.Bind();
        _shader.SetUniformInt("uUseIBL", 1);
        _shader.SetUniformFloat(
            "uMaxReflectionLod",
            Math.Max(environmentMap.PrefilteredMipCount - 1, 0)
        );

        _gl.ActiveTexture(TextureUnit.Texture6);
        _gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap.IrradianceMap);
        _gl.ActiveTexture(TextureUnit.Texture7);
        _gl.BindTexture(TextureTarget.TextureCubeMap, environmentMap.PrefilteredMap);
        _gl.ActiveTexture(TextureUnit.Texture8);
        _gl.BindTexture(TextureTarget.Texture2D, environmentMap.BrdfLut);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _shader.Unbind();
    }

    /// <summary>
    /// Disables image-based lighting: the PBR path falls back to the flat constant ambient term
    /// used before this feature existed. This is the default state until
    /// <see cref="SetEnvironmentMap"/> is called; scenes without a skybox never need to call this
    /// explicitly.
    /// </summary>
    public void DisableIBL()
    {
        _shader.Bind();
        _shader.SetUniformInt("uUseIBL", 0);
        _shader.Unbind();

        // Restore the default (complete) IBL textures. After a prior SetEnvironmentMap the units
        // may still point at textures owned by an EnvironmentMap that has since been disposed,
        // leaving the statically-used samplers incomplete even though sampling is gated off
        // (mirrors BindDefaultShadowTexture's reasoning in DisableShadows).
        BindDefaultIblTextures();
    }
}
