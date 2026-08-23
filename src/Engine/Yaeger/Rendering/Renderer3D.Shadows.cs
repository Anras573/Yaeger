using System.Numerics;
using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

// Directional shadow mapping (SetShadowMap/DisableShadows) and point-light cube shadow mapping
// (SetPointShadows/DisablePointShadows). See Renderer3D.Lighting.cs for the (separate) uniforms
// that describe the lights themselves, and Renderer3D.Textures.cs for the fallback textures these
// samplers fall back to when disabled.
public sealed partial class Renderer3D
{
    /// <summary>
    /// Maximum number of point lights that can cast a cube shadow map at once. Six face renders per
    /// light makes this expensive relative to the 16-light shading budget, so it's capped well
    /// below it — see <see cref="PointLight.CastsShadows"/> for what happens past the cap.
    /// </summary>
    public const int MaxShadowCastingPointLights = 2;

    // Point shadow slots: flat (non-struct-field) uniform array names/units, one per shadow-casting
    // slot rather than per point light — see SetPointShadows.
    private static readonly string[] PointShadowMapSamplerNames = BuildFlatNames(
        "uPointShadowMap",
        MaxShadowCastingPointLights
    );
    private static readonly string[] PointShadowCasterIndexNames = BuildFlatNames(
        "uPointShadowCasterIndex",
        MaxShadowCastingPointLights
    );
    private static readonly string[] PointShadowFarPlaneNames = BuildFlatNames(
        "uPointShadowFarPlane",
        MaxShadowCastingPointLights
    );
    private static readonly string[] PointShadowBiasNames = BuildFlatNames(
        "uPointShadowBias",
        MaxShadowCastingPointLights
    );
    private static readonly TextureUnit[] PointShadowTextureUnits = BuildPointShadowTextureUnits();

    // Past every other sampler this renderer uses (0-9 the material/shadow/IBL/scene-depth samplers).
    private static TextureUnit[] BuildPointShadowTextureUnits()
    {
        var units = new TextureUnit[MaxShadowCastingPointLights];
        for (var slot = 0; slot < MaxShadowCastingPointLights; slot++)
            units[slot] = TextureUnit.Texture10 + slot;
        return units;
    }

    /// <summary>
    /// Binds the shadow map and uploads the light-space transform for the lighting pass. Call once
    /// per frame, after the shadow pass has populated the depth texture and before the draw loop.
    /// </summary>
    /// <param name="lightIndex">
    /// Index, into the span given to <see cref="SetSceneLighting(ReadOnlySpan{DirectionalLight}, Vector3)"/>,
    /// of the light this map was rendered from. There is one map, so only that light's contribution
    /// is shadowed — a second light sampling the same depths would darken with the wrong geometry.
    /// </param>
    /// <param name="strength">
    /// How far a shadowed fragment darkens, clamped to <c>[0, 1]</c>: 1 is a full shadow, 0 leaves
    /// everything lit. Values between let a setting sun fade its shadows out over several frames
    /// instead of switching them off in one. Defaults to 1 — the behaviour before it existed.
    /// </param>
    public void SetShadowMap(
        Matrix4x4 lightSpaceMatrix,
        uint depthTexture,
        float bias,
        bool enablePcf,
        int lightIndex = 0,
        float strength = 1f
    )
    {
        _shader.Bind();
        _shader.SetUniformInt(
            "uShadowLightIndex",
            lightIndex >= 0 && lightIndex < MaxDirectionalLights ? lightIndex : -1
        );
        _shader.SetUniformFloat(
            "uShadowStrength",
            float.IsFinite(strength) ? Math.Clamp(strength, 0f, 1f) : 0f
        );
        _shader.SetUniformMatrix4("uLightSpaceMatrix", lightSpaceMatrix);
        _shader.SetUniformFloat("uShadowBias", SanitizeNonNegative(bias));
        _shader.SetUniformInt("uUsePcf", enablePcf ? 1 : 0);
        _shader.SetUniformInt("uShadowsEnabled", 1);

        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, depthTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);

        _shader.Unbind();
    }

    /// <summary>
    /// Disables shadow sampling: the lighting pass treats every fragment as fully lit. This is the
    /// default state until <see cref="SetShadowMap"/> is called.
    /// </summary>
    public void DisableShadows()
    {
        _shader.Bind();
        _shader.SetUniformInt("uShadowsEnabled", 0);
        _shader.SetUniformMatrix4("uLightSpaceMatrix", Matrix4x4.Identity);
        _shader.SetUniformInt("uShadowLightIndex", -1);
        _shader.SetUniformFloat("uShadowStrength", 1f);
        _shader.Unbind();

        // Restore the default (complete) shadow texture on unit 5. After a prior SetShadowMap the
        // unit may still point at a depth texture that gets deleted when its ShadowMapRenderer is
        // disposed, leaving the statically-used sampler incomplete even though sampling is gated off.
        BindDefaultShadowTexture();
    }

    // The shadow sampler (unit 5) is statically used by the fragment shader, so it must point at a
    // complete texture even when shadows are disabled. Bind the 1×1 white texture (sampled as depth
    // 1.0 = fully lit) until SetShadowMap swaps in a real depth map. As with the PBR fallbacks, the
    // shadow path never unbinds this unit, so the one-time bind keeps it valid for the lifetime.
    private void BindDefaultShadowTexture()
    {
        _gl.ActiveTexture(TextureUnit.Texture5);
        _gl.BindTexture(TextureTarget.Texture2D, _defaultTexture);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    // The point shadow samplers (units 10/11) are statically used by the fragment shader the same
    // way uShadowMap is, so each must point at a complete texture even when its slot is unused.
    // Reuses the existing 1x1 white cubemap (already needed for the IBL fallback) — sampling its
    // .r channel back as "distance" reads 1.0 (the far plane), i.e. "no occluder found", which is
    // exactly the unshadowed default a disabled slot needs.
    private void BindDefaultPointShadowTexture(int slot)
    {
        _gl.ActiveTexture(PointShadowTextureUnits[slot]);
        _gl.BindTexture(TextureTarget.TextureCubeMap, _defaultCubemap);
        _gl.ActiveTexture(TextureUnit.Texture0);
    }

    /// <summary>One shadow-casting point light's cube shadow map, uploaded via <see cref="SetPointShadows"/>.</summary>
    /// <param name="LightIndex">
    /// Index into the span most recently passed to <see cref="SetPointLights"/> — only that light's
    /// contribution samples this slot's cubemap.
    /// </param>
    /// <param name="CubemapTexture">The depth cubemap <see cref="PointShadowMapRenderer.BeginFace"/> rendered into.</param>
    /// <param name="FarPlane">
    /// The far plane the cubemap was captured with — the casting light's own
    /// <see cref="PointLight.Range"/>, needed to turn the map's normalized stored distance back into
    /// a world-space one.
    /// </param>
    /// <param name="Bias">World-space depth bias for this slot's shadow test (see <see cref="PointShadowSettings.Bias"/>).</param>
    public readonly record struct PointShadowCaster(
        int LightIndex,
        uint CubemapTexture,
        float FarPlane,
        float Bias
    );

    /// <summary>
    /// Uploads the active point-light shadow casters for this frame. Call once per frame, after the
    /// point shadow pass has populated each slot's cubemap and before the draw loop — mirrors
    /// <see cref="SetShadowMap"/>'s place in the frame, extended to
    /// <see cref="MaxShadowCastingPointLights"/> independent slots instead of one. At most
    /// <see cref="MaxShadowCastingPointLights"/> casters are used; any extras are ignored (select
    /// which ones with <see cref="PointShadowMapRenderer.SelectShadowCasters"/> before calling this).
    /// Passing an empty span is <see cref="DisablePointShadows"/>.
    /// </summary>
    public void SetPointShadows(ReadOnlySpan<PointShadowCaster> casters)
    {
        _shader.Bind();
        for (var slot = 0; slot < MaxShadowCastingPointLights; slot++)
        {
            if (slot < casters.Length)
            {
                var caster = casters[slot];
                _shader.SetUniformInt(PointShadowCasterIndexNames[slot], caster.LightIndex);
                _shader.SetUniformFloat(
                    PointShadowFarPlaneNames[slot],
                    MathF.Max(SanitizeNonNegative(caster.FarPlane), 1e-4f)
                );
                _shader.SetUniformFloat(
                    PointShadowBiasNames[slot],
                    SanitizeNonNegative(caster.Bias)
                );

                _gl.ActiveTexture(PointShadowTextureUnits[slot]);
                _gl.BindTexture(TextureTarget.TextureCubeMap, caster.CubemapTexture);
                _gl.ActiveTexture(TextureUnit.Texture0);
            }
            else
            {
                _shader.SetUniformInt(PointShadowCasterIndexNames[slot], -1);
                BindDefaultPointShadowTexture(slot);
            }
        }
        _shader.Unbind();
    }

    /// <summary>
    /// Disables point-light shadow sampling: every point light's contribution is treated as fully
    /// lit. This is the default state until <see cref="SetPointShadows"/> is called with a non-empty
    /// span.
    /// </summary>
    public void DisablePointShadows() => SetPointShadows([]);
}
