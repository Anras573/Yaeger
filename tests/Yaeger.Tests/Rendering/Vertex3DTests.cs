using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

public class Vertex3DTests
{
    [Fact]
    public void ObjectInitializer_SetsAllFields()
    {
        var position = new Vector3(1f, 2f, 3f);
        var normal = new Vector3(0f, 1f, 0f);
        var texCoord = new Vector2(0.5f, 0.25f);

        var vertex = new Vertex3D
        {
            Position = position,
            Normal = normal,
            TexCoord = texCoord,
        };

        Assert.Equal(position, vertex.Position);
        Assert.Equal(normal, vertex.Normal);
        Assert.Equal(texCoord, vertex.TexCoord);
    }

    [Fact]
    public void Default_HasZeroValues()
    {
        var vertex = default(Vertex3D);

        Assert.Equal(Vector3.Zero, vertex.Position);
        Assert.Equal(Vector3.Zero, vertex.Normal);
        Assert.Equal(Vector2.Zero, vertex.TexCoord);
    }
}
