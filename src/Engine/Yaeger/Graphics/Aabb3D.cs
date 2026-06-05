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
}
