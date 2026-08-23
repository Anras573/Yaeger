using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Physics;

namespace Yaeger.Tests.Physics;

public class RayAabbIntersectionTests
{
    private static bool Near(Vector3 a, Vector3 b, float epsilon = 1e-3f) =>
        Vector3.Distance(a, b) < epsilon;

    [Fact]
    public void TryIntersect_RayHitsBoxFace_ReturnsOutwardNormal()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = RayAabbIntersection.TryIntersect(
            new Vector3(0, 0, 5),
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out var distance,
            out var normal
        );

        Assert.True(hit);
        Assert.Equal(4f, distance, 3);
        Assert.True(Near(new Vector3(0, 0, 1), normal));
    }

    [Fact]
    public void TryIntersect_RayOriginatesInsideBox_ReturnsExitFaceNormal()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = RayAabbIntersection.TryIntersect(
            Vector3.Zero,
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out var distance,
            out var normal
        );

        Assert.True(hit);
        Assert.Equal(1f, distance, 3);
        Assert.True(Near(new Vector3(0, 0, -1), normal));
    }

    [Fact]
    public void TryIntersect_RotatedBox_ReturnsRotatedNormal()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));
        // Rotate the box 90 degrees about Y: what was the +Z face now faces +X.
        var model = Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        var hit = RayAabbIntersection.TryIntersect(
            new Vector3(5, 0, 0),
            new Vector3(-1, 0, 0),
            box,
            model,
            out _,
            out var normal
        );

        Assert.True(hit);
        Assert.True(Near(new Vector3(1, 0, 0), normal));
    }

    [Fact]
    public void TryIntersect_RayMisses_NormalIsZero()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = RayAabbIntersection.TryIntersect(
            new Vector3(10, 0, 5),
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out _,
            out var normal
        );

        Assert.False(hit);
        Assert.Equal(Vector3.Zero, normal);
    }

    [Fact]
    public void TryIntersect_ZeroThicknessBounds_HitsExactPlane()
    {
        var box = new Aabb3D(new Vector3(-1, -1, 0), new Vector3(1, 1, 0));

        var hit = RayAabbIntersection.TryIntersect(
            new Vector3(0, 0, 5),
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out var distance,
            out _
        );

        Assert.True(hit);
        Assert.Equal(5f, distance, 3);
    }
}

public class Ray3DTests
{
    [Fact]
    public void Constructor_NormalizesDirection()
    {
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, 5));

        Assert.Equal(new Vector3(0, 0, 1), ray.Direction);
    }

    [Fact]
    public void Constructor_ZeroDirection_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ray3D(Vector3.Zero, Vector3.Zero));
    }
}
