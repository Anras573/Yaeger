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

    // ── Transform / Union / BoundingSphere ───────────────────────────────────

    [Fact]
    public void Transform_Translation_MovesTheBox()
    {
        var box = new Aabb3D(new Vector3(-1f), new Vector3(1f));

        var moved = box.Transform(Matrix4x4.CreateTranslation(new Vector3(5f, 0f, -3f)));

        Assert.Equal(new Vector3(4f, -1f, -4f), moved.Min);
        Assert.Equal(new Vector3(6f, 1f, -2f), moved.Max);
    }

    [Fact]
    public void Transform_Scale_GrowsTheBox()
    {
        var box = new Aabb3D(new Vector3(-1f), new Vector3(1f));

        var scaled = box.Transform(Matrix4x4.CreateScale(2f, 3f, 4f));

        Assert.Equal(new Vector3(-2f, -3f, -4f), scaled.Min);
        Assert.Equal(new Vector3(2f, 3f, 4f), scaled.Max);
    }

    [Fact]
    public void Transform_Rotation_ReBoundsToAnAxisAlignedBox()
    {
        var box = new Aabb3D(new Vector3(-1f, -1f, -1f), new Vector3(1f, 1f, 1f));

        // A 45-degree turn about Y widens the axis-aligned bounds to the cube's diagonal.
        var rotated = box.Transform(Matrix4x4.CreateRotationY(MathF.PI / 4f));

        Assert.Equal(MathF.Sqrt(2f), rotated.Max.X, precision: 4);
        Assert.Equal(1f, rotated.Max.Y, precision: 4);
    }

    [Fact]
    public void Union_ProducesTheSmallestBoxContainingBoth()
    {
        var a = new Aabb3D(new Vector3(-1f), new Vector3(1f));
        var b = new Aabb3D(new Vector3(2f, -3f, 0f), new Vector3(4f, 0f, 1f));

        var union = a.Union(b);

        Assert.Equal(new Vector3(-1f, -3f, -1f), union.Min);
        Assert.Equal(new Vector3(4f, 1f, 1f), union.Max);
    }

    [Fact]
    public void Union_IsCommutative()
    {
        var a = new Aabb3D(new Vector3(-1f), new Vector3(1f));
        var b = new Aabb3D(new Vector3(2f, -3f, 0f), new Vector3(4f, 0f, 1f));

        Assert.Equal(a.Union(b), b.Union(a));
    }

    [Fact]
    public void BoundingSphere_EnclosesEveryCorner()
    {
        var box = new Aabb3D(new Vector3(1f, 2f, 3f), new Vector3(5f, 4f, 9f));

        var (center, radius) = box.BoundingSphere();

        Assert.Equal(new Vector3(3f, 3f, 6f), center);
        foreach (var x in new[] { box.Min.X, box.Max.X })
        foreach (var y in new[] { box.Min.Y, box.Max.Y })
        foreach (var z in new[] { box.Min.Z, box.Max.Z })
            Assert.True((new Vector3(x, y, z) - center).Length() <= radius + 1e-4f);
    }

    [Fact]
    public void BoundingSphere_OfADegenerateBox_HasZeroRadius()
    {
        var (_, radius) = new Aabb3D(Vector3.Zero, Vector3.Zero).BoundingSphere();

        Assert.Equal(0f, radius);
    }
}
