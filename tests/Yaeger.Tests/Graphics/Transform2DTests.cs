using System.Numerics;

using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class Transform2DTests
{
    [Fact]
    public void Constructor_ShouldSetPosition()
    {
        // Arrange & Act
        var transform = new Transform2D(new Vector2(10, 20));

        // Assert
        Assert.Equal(10, transform.Position.X);
        Assert.Equal(20, transform.Position.Y);
    }

    [Fact]
    public void Constructor_ShouldSetRotation()
    {
        // Arrange & Act
        var transform = new Transform2D(new Vector2(0, 0), rotation: 1.5f);

        // Assert
        Assert.Equal(1.5f, transform.Rotation);
    }

    [Fact]
    public void Constructor_ShouldDefaultRotationToZero()
    {
        // Arrange & Act
        var transform = new Transform2D(new Vector2(0, 0));

        // Assert
        Assert.Equal(0f, transform.Rotation);
    }

    [Fact]
    public void Constructor_ShouldSetScale()
    {
        // Arrange & Act
        var transform = new Transform2D(new Vector2(0, 0), scale: new Vector2(2, 3));

        // Assert
        Assert.Equal(2, transform.Scale.X);
        Assert.Equal(3, transform.Scale.Y);
    }

    [Fact]
    public void Constructor_ShouldDefaultScaleToOne()
    {
        // Arrange & Act
        var transform = new Transform2D(new Vector2(0, 0));

        // Assert
        Assert.Equal(Vector2.One, transform.Scale);
    }

    [Fact]
    public void TransformMatrix_ShouldIncludeScale()
    {
        // Arrange
        var transform = new Transform2D(new Vector2(0, 0), scale: new Vector2(2, 3));

        // Act
        var matrix = transform.TransformMatrix;

        // Assert
        // First column represents scaling in X
        Assert.Equal(2, matrix.M11);
        // Second column represents scaling in Y
        Assert.Equal(3, matrix.M22);
    }

    [Fact]
    public void TransformMatrix_ShouldIncludeTranslation()
    {
        // Arrange
        var transform = new Transform2D(new Vector2(10, 20));

        // Act
        var matrix = transform.TransformMatrix;

        // Assert
        Assert.Equal(10, matrix.M41);
        Assert.Equal(20, matrix.M42);
    }

    [Fact]
    public void TransformMatrix_ShouldIncludeRotation()
    {
        // Arrange
        var rotation = MathF.PI / 2; // 90 degrees
        var transform = new Transform2D(new Vector2(0, 0), rotation: rotation);

        // Act
        var matrix = transform.TransformMatrix;

        // Assert
        // The transform matrix applies scale, then rotation, then translation
        // After 90 degree rotation the matrix should have rotated components
        // We just check that rotation has affected the matrix (it's not identity)
        var hasRotation = MathF.Abs(matrix.M11 - 1.0f) > 0.01f ||
                         MathF.Abs(matrix.M12) > 0.01f ||
                         MathF.Abs(matrix.M21) > 0.01f ||
                         MathF.Abs(matrix.M22 - 1.0f) > 0.01f;
        Assert.True(hasRotation);
    }

    [Fact]
    public void TransformMatrix_ShouldCombineAllTransformations()
    {
        // Arrange
        var transform = new Transform2D(
            new Vector2(10, 20),
            rotation: 0f,
            scale: new Vector2(2, 2));

        // Act
        var matrix = transform.TransformMatrix;

        // Assert - Check that scale and translation are applied
        Assert.Equal(2, matrix.M11); // Scale X
        Assert.Equal(2, matrix.M22); // Scale Y
        Assert.Equal(10, matrix.M41); // Translation X
        Assert.Equal(20, matrix.M42); // Translation Y
    }

    [Fact]
    public void Transform2D_ShouldAllowMutatingPosition()
    {
        // Arrange
        var transform = new Transform2D(new Vector2(0, 0));

        // Act
        transform.Position = new Vector2(5, 10);

        // Assert
        Assert.Equal(5, transform.Position.X);
        Assert.Equal(10, transform.Position.Y);
    }

    [Fact]
    public void Transform2D_ShouldAllowMutatingRotation()
    {
        // Arrange
        var transform = new Transform2D(new Vector2(0, 0));

        // Act
        transform.Rotation = 2.5f;

        // Assert
        Assert.Equal(2.5f, transform.Rotation);
    }

    [Fact]
    public void Transform2D_ShouldAllowMutatingScale()
    {
        // Arrange
        var transform = new Transform2D(new Vector2(0, 0));

        // Act
        transform.Scale = new Vector2(3, 4);

        // Assert
        Assert.Equal(3, transform.Scale.X);
        Assert.Equal(4, transform.Scale.Y);
    }
}