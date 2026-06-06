using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

public class MeshDataTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var vertices = new[] { new Vertex3D(Vector3.UnitX, Vector3.UnitY, Vector2.Zero) };
        var indices = new uint[] { 0 };

        var mesh = new MeshData("cube", vertices, indices);

        Assert.Equal("cube", mesh.Name);
        Assert.Same(vertices, mesh.Vertices);
        Assert.Same(indices, mesh.Indices);
    }

    [Fact]
    public void RecordEquality_SameReferences_AreEqual()
    {
        var vertices = new[] { new Vertex3D(Vector3.Zero, Vector3.Zero, Vector2.Zero) };
        var indices = new uint[] { 0 };

        var a = new MeshData("mesh", vertices, indices);
        var b = new MeshData("mesh", vertices, indices);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentArrayInstances_AreNotEqual()
    {
        // Arrays do not override Equals, so different instances are not equal
        // even when the contents match.
        var a = new MeshData("mesh", new Vertex3D[0], new uint[] { 0 });
        var b = new MeshData("mesh", new Vertex3D[0], new uint[] { 0 });

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void With_ProducesUpdatedRecord()
    {
        var vertices = new Vertex3D[3];
        var indices = new uint[] { 0, 1, 2 };
        var original = new MeshData("original", vertices, indices);

        var renamed = original with { Name = "renamed" };

        Assert.Equal("renamed", renamed.Name);
        Assert.Same(vertices, renamed.Vertices);
        Assert.Same(indices, renamed.Indices);
    }

    [Fact]
    public void ToAabb_EmptyVertices_ReturnsZeroAabb()
    {
        var mesh = new MeshData("empty", Array.Empty<Vertex3D>(), Array.Empty<uint>());

        var aabb = mesh.ToAabb();

        Assert.Equal(Vector3.Zero, aabb.Min);
        Assert.Equal(Vector3.Zero, aabb.Max);
    }

    [Fact]
    public void ToAabb_SingleVertex_ReturnsDegenerateAabb()
    {
        var pos = new Vector3(1f, 2f, 3f);
        var mesh = new MeshData(
            "single",
            new[] { new Vertex3D(pos, Vector3.Zero, Vector2.Zero) },
            new uint[] { 0 }
        );

        var aabb = mesh.ToAabb();

        Assert.Equal(pos, aabb.Min);
        Assert.Equal(pos, aabb.Max);
    }

    [Fact]
    public void ToAabb_MultipleVertices_ReturnsCorrectBounds()
    {
        var vertices = new[]
        {
            new Vertex3D(new Vector3(-1f, 0f, 2f), Vector3.Zero, Vector2.Zero),
            new Vertex3D(new Vector3(3f, -2f, 0f), Vector3.Zero, Vector2.Zero),
            new Vertex3D(new Vector3(0f, 4f, 1f), Vector3.Zero, Vector2.Zero),
        };
        var mesh = new MeshData("box", vertices, new uint[] { 0, 1, 2 });

        var aabb = mesh.ToAabb();

        Assert.Equal(new Vector3(-1f, -2f, 0f), aabb.Min);
        Assert.Equal(new Vector3(3f, 4f, 2f), aabb.Max);
    }
}
