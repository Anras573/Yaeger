using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Graphics;

public class Aabb3DTests
{
    [Fact]
    public void Constructor_ShouldSetMinAndMax()
    {
        // Arrange & Act
        var aabb = new Aabb3D(new Vector3(-1, -2, -3), new Vector3(4, 5, 6));

        // Assert
        Assert.Equal(new Vector3(-1, -2, -3), aabb.Min);
        Assert.Equal(new Vector3(4, 5, 6), aabb.Max);
    }

    [Fact]
    public void FromVertices_SingleVertex_ShouldReturnZeroSizeBox()
    {
        // Arrange
        var vertex = new Vertex3D(new Vector3(1, 2, 3), Vector3.UnitY, Vector2.Zero);
        var vertices = new[] { vertex };

        // Act
        var aabb = Aabb3D.FromVertices(vertices);

        // Assert
        Assert.Equal(new Vector3(1, 2, 3), aabb.Min);
        Assert.Equal(new Vector3(1, 2, 3), aabb.Max);
    }

    [Fact]
    public void FromVertices_MultipleVertices_ShouldReturnCorrectBounds()
    {
        // Arrange
        var vertices = new[]
        {
            new Vertex3D(new Vector3(-1, -2, -3), Vector3.UnitY, Vector2.Zero),
            new Vertex3D(new Vector3(4, 5, 6), Vector3.UnitY, Vector2.Zero),
            new Vertex3D(new Vector3(0, 0, 0), Vector3.UnitY, Vector2.Zero),
            new Vertex3D(new Vector3(2, -5, 1), Vector3.UnitY, Vector2.Zero),
        };

        // Act
        var aabb = Aabb3D.FromVertices(vertices);

        // Assert
        Assert.Equal(new Vector3(-1, -5, -3), aabb.Min);
        Assert.Equal(new Vector3(4, 5, 6), aabb.Max);
    }

    [Fact]
    public void FromVertices_EmptyArray_ShouldReturnZeroAabb()
    {
        // Arrange
        var vertices = Array.Empty<Vertex3D>();

        // Act
        var aabb = Aabb3D.FromVertices(vertices);

        // Assert
        Assert.Equal(Vector3.Zero, aabb.Min);
        Assert.Equal(Vector3.Zero, aabb.Max);
    }

    [Fact]
    public void Aabb3D_ShouldSupportValueEquality()
    {
        // Arrange
        var a = new Aabb3D(Vector3.Zero, Vector3.One);
        var b = new Aabb3D(Vector3.Zero, Vector3.One);

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void Aabb3D_ShouldDetectInequality()
    {
        // Arrange
        var a = new Aabb3D(Vector3.Zero, Vector3.One);
        var b = new Aabb3D(Vector3.Zero, new Vector3(2, 2, 2));

        // Assert
        Assert.NotEqual(a, b);
    }
}
