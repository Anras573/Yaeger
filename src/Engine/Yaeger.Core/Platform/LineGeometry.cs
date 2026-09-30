using System.Numerics;

namespace Yaeger.Platform;

/// <summary>Pure math for turning a line segment into a unit-quad transform.</summary>
public static class LineGeometry
{
    /// <summary>
    /// Returns the transform that stretches the unit quad (centred on the origin) along the
    /// segment <paramref name="from"/> → <paramref name="to"/>, <paramref name="thickness"/> wide.
    /// A zero-length segment yields a zero-width quad (nothing visible).
    /// </summary>
    public static Matrix4x4 CreateTransform(Vector2 from, Vector2 to, float thickness)
    {
        var delta = to - from;
        var midpoint = (from + to) * 0.5f;
        return Matrix4x4.CreateScale(delta.Length(), thickness, 1f)
            * Matrix4x4.CreateRotationZ(MathF.Atan2(delta.Y, delta.X))
            * Matrix4x4.CreateTranslation(midpoint.X, midpoint.Y, 0f);
    }
}
