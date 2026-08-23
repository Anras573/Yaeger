using System.Numerics;

namespace Yaeger.Physics;

/// <summary>
/// A world-space ray for 3D queries: an origin point and a direction, normalized on construction
/// so every query built on top of it (<see cref="WorldRaycastExtensions"/>) can assume unit
/// length rather than re-checking it. Mirrors the origin+direction shape
/// <c>Yaeger.Inspector.ViewportPicking.TryGetPickRay</c> already produces for click-to-select.
/// </summary>
public readonly record struct Ray3D
{
    public Vector3 Origin { get; }
    public Vector3 Direction { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="direction"/> is zero-length, near-zero, or non-finite.
    /// </exception>
    public Ray3D(Vector3 origin, Vector3 direction)
    {
        var lengthSq = direction.LengthSquared();
        if (lengthSq < 1e-10f || !float.IsFinite(lengthSq))
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Direction must be a non-zero, finite vector."
            );

        Origin = origin;
        Direction = direction / MathF.Sqrt(lengthSq);
    }
}
