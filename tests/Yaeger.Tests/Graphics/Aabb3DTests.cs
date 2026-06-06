using System.Numerics;
using Yaeger.Graphics;

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
    public void FromPositions_SinglePosition_ShouldReturnZeroSizeBox()
    {
        // Arrange
        ReadOnlySpan<Vector3> positions = [new Vector3(1, 2, 3)];

        // Act
        var aabb = Aabb3D.FromPositions(positions);

        // Assert
        Assert.Equal(new Vector3(1, 2, 3), aabb.Min);
        Assert.Equal(new Vector3(1, 2, 3), aabb.Max);
    }

    [Fact]
    public void FromPositions_MultiplePositions_ShouldReturnCorrectBounds()
    {
        // Arrange
        ReadOnlySpan<Vector3> positions =
        [
            new Vector3(-1, -2, -3),
            new Vector3(4, 5, 6),
            new Vector3(0, 0, 0),
            new Vector3(2, -5, 1),
        ];

        // Act
        var aabb = Aabb3D.FromPositions(positions);

        // Assert
        Assert.Equal(new Vector3(-1, -5, -3), aabb.Min);
        Assert.Equal(new Vector3(4, 5, 6), aabb.Max);
    }

    [Fact]
    public void FromPositions_EmptySpan_ShouldReturnZeroAabb()
    {
        // Arrange
        ReadOnlySpan<Vector3> positions = [];

        // Act
        var aabb = Aabb3D.FromPositions(positions);

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
