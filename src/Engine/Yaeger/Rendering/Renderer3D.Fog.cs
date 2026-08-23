using Yaeger.Graphics;

namespace Yaeger.Rendering;

// Distance fog: a depth-dependent tint mixed into every fragment after lighting and emissive.
public sealed partial class Renderer3D
{
    /// <summary>
    /// Enables distance fog and uploads its parameters. Call once per frame, before the draw loop.
    /// Applied identically in the PBR and Blinn-Phong branches, after lighting and emissive, before
    /// the alpha write — so opaque, transparent (<see cref="BeginTransparentPass"/>), and additive
    /// surfaces at the same depth fog consistently. The skybox is unaffected (see
    /// <see cref="FogSettings"/>'s remarks).
    /// </summary>
    public void SetFog(FogSettings fog)
    {
        _shader.Bind();
        _shader.SetUniformInt("uFogEnabled", 1);
        _shader.SetUniformVec4("uFogColor", fog.Color.ToVector4());
        _shader.SetUniformInt("uFogMode", (int)fog.Mode);
        _shader.SetUniformFloat("uFogDensity", SanitizeNonNegative(fog.Density));
        _shader.SetUniformFloat("uFogStart", SanitizeNonNegative(fog.Start));
        // End must not land at or below Start: the shader divides by (End - Start), and a
        // degenerate/inverted range would otherwise divide by zero or invert the falloff.
        _shader.SetUniformFloat(
            "uFogEnd",
            MathF.Max(SanitizeNonNegative(fog.End), SanitizeNonNegative(fog.Start) + 1e-3f)
        );
        _shader.Unbind();
    }

    /// <summary>
    /// Disables distance fog: fragments render exactly as they would without this feature. This is
    /// the default state until <see cref="SetFog"/> is called; scenes that never enable fog never
    /// need to call this explicitly — it exists so a shared <see cref="Renderer3D"/> doesn't leak
    /// fog from a previous scene into one that doesn't want it (mirrors <see cref="DisableShadows"/>
    /// and <see cref="DisableIBL"/>).
    /// </summary>
    public void DisableFog()
    {
        _shader.Bind();
        _shader.SetUniformInt("uFogEnabled", 0);
        _shader.Unbind();
    }
}
