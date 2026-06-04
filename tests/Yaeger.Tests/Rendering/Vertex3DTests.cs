using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

public class Vertex3DTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        var position = new Vector3(1f, 2f, 3f);
        var normal = new Vector3(0f, 1f, 0f);
        var texCoord = new Vector2(0.5f, 0.25f);
        var tangent = new Vector3(1f, 0f, 0f);

        var vertex = new Vertex3D(position, normal, texCoord, tangent);

        Assert.Equal(position, vertex.Position);
        Assert.Equal(normal, vertex.Normal);
        Assert.Equal(texCoord, vertex.TexCoord);
        Assert.Equal(tangent, vertex.Tangent);
    }

    [Fact]
    public void Constructor_TangentDefaultsToZero()
    {
        var vertex = new Vertex3D(Vector3.UnitX, Vector3.UnitY, Vector2.Zero);

        Assert.Equal(Vector3.Zero, vertex.Tangent);
    }

    [Fact]
    public void Default_HasZeroValues()
    {
        var vertex = default(Vertex3D);

        Assert.Equal(Vector3.Zero, vertex.Position);
        Assert.Equal(Vector3.Zero, vertex.Normal);
        Assert.Equal(Vector2.Zero, vertex.TexCoord);
        Assert.Equal(Vector3.Zero, vertex.Tangent);
    }
}
