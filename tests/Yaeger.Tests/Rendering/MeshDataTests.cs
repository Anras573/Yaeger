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
}
