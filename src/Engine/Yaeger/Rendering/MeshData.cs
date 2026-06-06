using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

public record MeshData(string Name, Vertex3D[] Vertices, uint[] Indices)
{
    public Aabb3D ToAabb()
    {
        if (Vertices.Length == 0)
            return new Aabb3D(Vector3.Zero, Vector3.Zero);

        var min = Vertices[0].Position;
        var max = Vertices[0].Position;

        for (var i = 1; i < Vertices.Length; i++)
        {
            var p = Vertices[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new Aabb3D(min, max);
    }
}
