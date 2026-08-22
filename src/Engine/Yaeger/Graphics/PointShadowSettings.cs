namespace Yaeger.Graphics;

/// <summary>
/// Configuration shared by every point light's cube shadow map. Unlike <see cref="ShadowSettings"/>,
/// there's no orthographic extent or far plane to tune — each light's own <see cref="PointLight.Range"/>
/// is the shadow far plane, since it already bounds how far the light reaches.
/// </summary>
/// <remarks>
/// Like <see cref="ShadowSettings"/>, <c>default(PointShadowSettings)</c> is all-zero (a degenerate
/// map) rather than sensible — start from <see cref="Default"/> and override what you need with a
/// <c>with</c> expression.
/// </remarks>
public record struct PointShadowSettings
{
    /// <summary>
    /// Square resolution, in texels, of each of a point shadow cubemap's six faces. Six faces per
    /// light makes this expensive to raise — modest values (a few hundred texels) are plenty for a
    /// small point light close to what it's illuminating.
    /// </summary>
    public int MapResolution;

    /// <summary>Near plane of each face's perspective capture.</summary>
    public float NearPlane;

    /// <summary>
    /// World-space depth bias subtracted during the shadow test to prevent shadow acne. Unlike
    /// <see cref="ShadowSettings.Bias"/> (a normalized-depth bias), this is a plain world-space
    /// distance, since a point shadow's stored value is linear distance from the light rather than
    /// device depth.
    /// </summary>
    public float Bias;

    public static PointShadowSettings Default =>
        new()
        {
            MapResolution = 512,
            NearPlane = 0.05f,
            Bias = 0.05f,
        };
}
