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

    public static PointLight Default =>
        new()
        {
            Color = Color.White,
            Intensity = 1f,
            Range = 10f,
        };
}
