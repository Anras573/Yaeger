using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Graphics;

public record struct Aabb3D(Vector3 Min, Vector3 Max)
{
    public static Aabb3D FromVertices(Vertex3D[] vertices)
    {
        if (vertices.Length == 0)
            return new Aabb3D(Vector3.Zero, Vector3.Zero);

        var min = vertices[0].Position;
        var max = vertices[0].Position;

        for (var i = 1; i < vertices.Length; i++)
        {
            var p = vertices[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new Aabb3D(min, max);
    }
}
