using System.Numerics;
using Yaeger.Rendering;

namespace SponzaNight;

internal static class MeshFactory
{
    // Creates a tapered cylinder (a cone frustum when radiusBottom != radiusTop) with capped ends,
    // centred on the origin and extending from -height/2 to +height/2 along Y — the brazier's
    // pedestal and bowl are each one of these, stacked via Transform3D. Matches
    // Samples/SequenceDemo/MeshFactory.CreateBox's shape: CCW winding viewed from the outward
    // normal, smooth per-vertex side normals (a bowl reads as curved, not faceted).
    public static MeshData CreateCylinder(
        string name,
        float radiusBottom,
        float radiusTop,
        float height,
        int segments = 24
    )
    {
        var vertices = new List<Vertex3D>();
        var indices = new List<uint>();
        var halfHeight = height * 0.5f;

        // Side wall: two rings of vertices (bottom, top), each vertex's normal tilted to account for
        // the taper between radiusBottom and radiusTop, so shading is smooth across the slope.
        var slope = MathF.Atan2(radiusBottom - radiusTop, height);
        var normalY = MathF.Sin(slope);
        var normalXZ = MathF.Cos(slope);

        var bottomRingStart = (uint)vertices.Count;
        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.Tau;
            var (sin, cos) = (MathF.Sin(angle), MathF.Cos(angle));
            var normal = new Vector3(cos * normalXZ, normalY, sin * normalXZ);
            var position = new Vector3(cos * radiusBottom, -halfHeight, sin * radiusBottom);
            var tangent = new Vector3(-sin, 0f, cos);
            vertices.Add(new Vertex3D(position, normal, new Vector2(t, 0f), tangent));
        }

        var topRingStart = (uint)vertices.Count;
        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * MathF.Tau;
            var (sin, cos) = (MathF.Sin(angle), MathF.Cos(angle));
            var normal = new Vector3(cos * normalXZ, normalY, sin * normalXZ);
            var position = new Vector3(cos * radiusTop, halfHeight, sin * radiusTop);
            var tangent = new Vector3(-sin, 0f, cos);
            vertices.Add(new Vertex3D(position, normal, new Vector2(t, 1f), tangent));
        }

        for (var i = 0; i < segments; i++)
        {
            var b0 = bottomRingStart + (uint)i;
            var b1 = bottomRingStart + (uint)i + 1;
            var t0 = topRingStart + (uint)i;
            var t1 = topRingStart + (uint)i + 1;
            indices.AddRange([b0, t0, t1, b0, t1, b1]);
        }

        // End caps — flat-shaded fans, skipped for a zero radius (a cone's apex needs no cap).
        void AddCap(float y, float radius, Vector3 normal, bool flip)
        {
            if (radius <= 0f)
                return;

            var center = (uint)vertices.Count;
            vertices.Add(
                new Vertex3D(new Vector3(0f, y, 0f), normal, new Vector2(0.5f), Vector3.UnitX)
            );
            var ringStart = (uint)vertices.Count;
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var angle = t * MathF.Tau;
                var position = new Vector3(MathF.Cos(angle) * radius, y, MathF.Sin(angle) * radius);
                var uv = new Vector2(
                    0.5f + MathF.Cos(angle) * 0.5f,
                    0.5f + MathF.Sin(angle) * 0.5f
                );
                vertices.Add(new Vertex3D(position, normal, uv, Vector3.UnitX));
            }

            for (var i = 0; i < segments; i++)
            {
                var a = ringStart + (uint)i;
                var b = ringStart + (uint)i + 1;
                uint[] triangle = flip ? [center, b, a] : [center, a, b];
                indices.AddRange(triangle);
            }
        }

        AddCap(-halfHeight, radiusBottom, -Vector3.UnitY, flip: true);
        AddCap(halfHeight, radiusTop, Vector3.UnitY, flip: false);

        return new MeshData(name, [.. vertices], [.. indices]);
    }
}
