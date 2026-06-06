using System.Numerics;
using Yaeger.Rendering;

namespace CornellBox;

internal static class MeshFactory
{
    // Creates a flat quad from 4 vertices in CCW order (when viewed from the normal side).
    // Tangent is inferred from the first edge.
    public static MeshData CreateQuad(
        string name,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        Vector3 v3,
        Vector3 normal
    )
    {
        var tangent = Vector3.Normalize(v1 - v0);
        return new MeshData(
            name,
            [
                new Vertex3D(v0, normal, new Vector2(0f, 0f), tangent),
                new Vertex3D(v1, normal, new Vector2(1f, 0f), tangent),
                new Vertex3D(v2, normal, new Vector2(1f, 1f), tangent),
                new Vertex3D(v3, normal, new Vector2(0f, 1f), tangent),
            ],
            [0u, 1u, 2u, 0u, 2u, 3u]
        );
    }

    // Creates a unit box centred at the origin with outward-facing normals on all six faces.
    // Scale, rotate, and position via Transform3D to get the desired shape and placement.
    public static MeshData CreateBox(string name)
    {
        var vertices = new List<Vertex3D>(24);
        var indices = new List<uint>(36);

        void AddFace(Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = (uint)vertices.Count;
            var t = Vector3.Normalize(b - a);
            vertices.Add(new Vertex3D(a, n, new Vector2(0f, 0f), t));
            vertices.Add(new Vertex3D(b, n, new Vector2(1f, 0f), t));
            vertices.Add(new Vertex3D(c, n, new Vector2(1f, 1f), t));
            vertices.Add(new Vertex3D(d, n, new Vector2(0f, 1f), t));
            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }

        // Vertex order is CCW when viewed from the outward normal direction.
        AddFace(
            Vector3.UnitX,
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f)
        );

        AddFace(
            -Vector3.UnitX,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f)
        );

        AddFace(
            Vector3.UnitY,
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f)
        );

        AddFace(
            -Vector3.UnitY,
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f)
        );

        AddFace(
            Vector3.UnitZ,
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        );

        AddFace(
            -Vector3.UnitZ,
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f)
        );

        return new MeshData(name, [.. vertices], [.. indices]);
    }
}
