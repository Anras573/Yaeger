using System.Linq;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

public record MeshData(string Name, Vertex3D[] Vertices, uint[] Indices)
{
    public Aabb3D ToAabb() =>
        Aabb3D.FromPositions(Vertices.Select(static v => v.Position).ToArray());
}
