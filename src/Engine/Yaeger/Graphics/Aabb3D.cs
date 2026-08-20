using System.Numerics;

namespace Yaeger.Graphics;

public record struct Aabb3D(Vector3 Min, Vector3 Max)
{
    public static Aabb3D FromPositions(ReadOnlySpan<Vector3> positions)
    {
        if (positions.IsEmpty)
            return new Aabb3D(Vector3.Zero, Vector3.Zero);

        var min = positions[0];
        var max = positions[0];

        for (var i = 1; i < positions.Length; i++)
        {
            min = Vector3.Min(min, positions[i]);
            max = Vector3.Max(max, positions[i]);
        }

        return new Aabb3D(min, max);
    }

    /// <summary>Centre point of the box.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>
    /// The world-space axis-aligned box enclosing this (local-space) box transformed by
    /// <paramref name="model"/> — all eight corners transformed, then re-bounded. Rotating a box and
    /// re-bounding it grows it, which is the price of staying axis-aligned; this mirrors what
    /// <see cref="CameraFrustum.Intersects"/> does per corner for culling.
    /// </summary>
    public Aabb3D Transform(Matrix4x4 model)
    {
        Span<Vector3> corners =
        [
            Vector3.Transform(new Vector3(Min.X, Min.Y, Min.Z), model),
            Vector3.Transform(new Vector3(Max.X, Min.Y, Min.Z), model),
            Vector3.Transform(new Vector3(Min.X, Max.Y, Min.Z), model),
            Vector3.Transform(new Vector3(Max.X, Max.Y, Min.Z), model),
            Vector3.Transform(new Vector3(Min.X, Min.Y, Max.Z), model),
            Vector3.Transform(new Vector3(Max.X, Min.Y, Max.Z), model),
            Vector3.Transform(new Vector3(Min.X, Max.Y, Max.Z), model),
            Vector3.Transform(new Vector3(Max.X, Max.Y, Max.Z), model),
        ];

        return FromPositions(corners);
    }

    /// <summary>The smallest box containing both this one and <paramref name="other"/>.</summary>
    public Aabb3D Union(Aabb3D other) =>
        new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <summary>
    /// A sphere enclosing this box: its centre, and the distance from there to a corner. Not the
    /// tightest sphere for the geometry inside, but it is the one a shadow frustum needs — it bounds
    /// the box from every direction, so a light can frame it from any angle.
    /// </summary>
    public (Vector3 Center, float Radius) BoundingSphere()
    {
        var center = Center;
        var radius = (Max - center).Length();
        return (center, float.IsFinite(radius) ? radius : 0f);
    }
}
