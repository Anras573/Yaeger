using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A cone-shaped light (a flashlight or stage spotlight). Attach alongside a
/// <see cref="Transform3D"/>; the transform's position places the cone's apex in the world while
/// <see cref="Direction"/> aims the beam. The edge of the cone fades smoothly between
/// <see cref="InnerConeAngle"/> (fully lit) and <see cref="OuterConeAngle"/> (fully dark).
/// <c>MeshRenderSystem</c> collects these entities each frame and uploads them to
/// <c>Renderer3D</c>.
/// </summary>
public record struct SpotLight
{
    public Color Color;
    public float Intensity;

    /// <summary>Direction the beam points, from the light outward; need not be pre-normalised.</summary>
    public Vector3 Direction;

    /// <summary>Half-angle of the inner (fully lit) cone, in radians.</summary>
    public float InnerConeAngle;

    /// <summary>Half-angle of the outer (fully dark) cone, in radians.</summary>
    public float OuterConeAngle;

    /// <summary>Distance at which the light's contribution falls to zero (world units).</summary>
    public float Range;

    public static SpotLight Default =>
        new()
        {
            Color = Color.White,
            Intensity = 1f,
            Direction = -Vector3.UnitY,
            InnerConeAngle = MathF.PI / 9f, // 20°
            OuterConeAngle = MathF.PI / 6f, // 30°
            Range = 10f,
        };
}
