using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class Transform3DTests
{
    [Fact]
    public void Constructor_ShouldSetPosition()
    {
        // Arrange & Act
        var transform = new Transform3D(new Vector3(1, 2, 3), Quaternion.Identity, Vector3.One);

        // Assert
        Assert.Equal(new Vector3(1, 2, 3), transform.Position);
    }

    [Fact]
    public void Constructor_ShouldSetRotation()
    {
        // Arrange & Act
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
        var transform = new Transform3D(Vector3.Zero, rotation, Vector3.One);

        // Assert
        Assert.Equal(rotation, transform.Rotation);
    }

    [Fact]
    public void Constructor_ShouldSetScale()
    {
        // Arrange & Act
        var transform = new Transform3D(Vector3.Zero, Quaternion.Identity, new Vector3(2, 3, 4));

        // Assert
        Assert.Equal(new Vector3(2, 3, 4), transform.Scale);
    }

    [Fact]
    public void Identity_ShouldHaveZeroPosition()
    {
        // Arrange & Act
        var transform = Transform3D.Identity;

        // Assert
        Assert.Equal(Vector3.Zero, transform.Position);
    }

    [Fact]
    public void Identity_ShouldHaveIdentityRotation()
    {
        // Arrange & Act
        var transform = Transform3D.Identity;

        // Assert
        Assert.Equal(Quaternion.Identity, transform.Rotation);
    }

    [Fact]
    public void Identity_ShouldHaveOneScale()
    {
        // Arrange & Act
        var transform = Transform3D.Identity;

        // Assert
        Assert.Equal(Vector3.One, transform.Scale);
    }

    [Fact]
    public void ModelMatrix_ShouldIncludeTranslation()
    {
        // Arrange
        var transform = new Transform3D(new Vector3(5, 6, 7), Quaternion.Identity, Vector3.One);

        // Act
        var matrix = transform.ModelMatrix;

        // Assert
        Assert.Equal(5, matrix.M41);
        Assert.Equal(6, matrix.M42);
        Assert.Equal(7, matrix.M43);
    }

    [Fact]
    public void ModelMatrix_ShouldIncludeScale()
    {
        // Arrange
        var transform = new Transform3D(Vector3.Zero, Quaternion.Identity, new Vector3(2, 3, 4));

        // Act
        var matrix = transform.ModelMatrix;

        // Assert
        Assert.Equal(2, matrix.M11);
        Assert.Equal(3, matrix.M22);
        Assert.Equal(4, matrix.M33);
    }

    [Fact]
    public void ModelMatrix_ShouldIncludeRotation()
    {
        // Arrange
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var transform = new Transform3D(Vector3.Zero, rotation, Vector3.One);

        // Act
        var matrix = transform.ModelMatrix;

        // Assert — a 90° Y-rotation should make the matrix non-identity
        var hasRotation =
            MathF.Abs(matrix.M11 - 1.0f) > 0.01f
            || MathF.Abs(matrix.M13) > 0.01f
            || MathF.Abs(matrix.M31) > 0.01f
            || MathF.Abs(matrix.M33 - 1.0f) > 0.01f;
        Assert.True(hasRotation);
    }

    [Fact]
    public void ModelMatrix_IdentityShouldBeIdentityMatrix()
    {
        // Arrange
        var transform = Transform3D.Identity;

        // Act
        var matrix = transform.ModelMatrix;

        // Assert
        Assert.Equal(Matrix4x4.Identity, matrix);
    }

    [Fact]
    public void Transform3D_ShouldAllowMutatingPosition()
    {
        // Arrange
        var transform = Transform3D.Identity;

        // Act
        transform.Position = new Vector3(10, 20, 30);

        // Assert
        Assert.Equal(new Vector3(10, 20, 30), transform.Position);
    }

    [Fact]
    public void Transform3D_ShouldAllowMutatingRotation()
    {
        // Arrange
        var transform = Transform3D.Identity;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI);

        // Act
        transform.Rotation = rotation;

        // Assert
        Assert.Equal(rotation, transform.Rotation);
    }

    [Fact]
    public void Transform3D_ShouldAllowMutatingScale()
    {
        // Arrange
        var transform = Transform3D.Identity;

        // Act
        transform.Scale = new Vector3(5, 6, 7);

        // Assert
        Assert.Equal(new Vector3(5, 6, 7), transform.Scale);
    }
}
