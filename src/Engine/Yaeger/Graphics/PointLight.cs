namespace Yaeger.Graphics;

/// <summary>
/// An omni-directional light that radiates equally in all directions from a point.
/// Attach alongside a <see cref="Transform3D"/>; the transform's position places the light in the
/// world. <c>MeshRenderSystem</c> collects these entities each frame and uploads them to
/// <c>Renderer3D</c>.
/// </summary>
public record struct PointLight
{
    public Color Color;
    public float Intensity;

    /// <summary>Distance at which the light's contribution falls to zero (world units).</summary>
    public float Range;

    /// <summary>
    /// Opt-in cube shadow map for this light. Casting is capped at
    /// <c>Renderer3D.MaxShadowCastingPointLights</c>: when more lights request it than the cap
    /// allows, the ones closest to the camera win (see <c>PointShadowMapRenderer.SelectShadowCasters</c>)
    /// — lights beyond the cap simply light the scene without casting, exactly as if this were
    /// false. False by default, so an existing scene's cost and appearance are unchanged. See
    /// docs/shadows.md#point-light-shadows.
    /// </summary>
    public bool CastsShadows;

    public static PointLight Default =>
        new()
        {
            Color = Color.White,
            Intensity = 1f,
            Range = 10f,
            CastsShadows = false,
        };
}
