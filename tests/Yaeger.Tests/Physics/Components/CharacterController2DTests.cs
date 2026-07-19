using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class CharacterController2DTests
{
    [Fact]
    public void Constructor_WithSize_ShouldSetSizeAndDefaultOffset()
    {
        var controller = new CharacterController2D(new Vector2(1, 2));

        Assert.Equal(new Vector2(1, 2), controller.Size);
        Assert.Equal(Vector2.Zero, controller.Offset);
    }

    [Fact]
    public void Constructor_WithWidthAndHeight_ShouldSetSize()
    {
        var controller = new CharacterController2D(1f, 2f);

        Assert.Equal(new Vector2(1, 2), controller.Size);
        Assert.Equal(Vector2.Zero, controller.Offset);
    }

    [Fact]
    public void HalfSize_ShouldReturnHalfOfSize()
    {
        var controller = new CharacterController2D(new Vector2(4, 6));

        Assert.Equal(new Vector2(2, 3), controller.HalfSize);
    }

    [Fact]
    public void Constructor_ShouldDefaultToStandardValues()
    {
        var controller = new CharacterController2D(new Vector2(1, 1));

        Assert.Equal(1f, controller.GravityScale);
        Assert.Equal(0f, controller.StepHeight);
        Assert.Equal(0, controller.Layer);
        Assert.Equal(BoxCollider2D.AllLayers, controller.CollidesWith);
    }

    [Fact]
    public void Constructor_ShouldDefaultContactStateToUngrounded()
    {
        var controller = new CharacterController2D(new Vector2(1, 1));

        Assert.False(controller.IsGrounded);
        Assert.False(controller.IsTouchingWallLeft);
        Assert.False(controller.IsTouchingWallRight);
        Assert.False(controller.IsTouchingCeiling);
        Assert.Equal(Vector2.Zero, controller.GroundNormal);
        Assert.Null(controller.GroundEntity);
    }

    [Fact]
    public void Constructor_ShouldSetCustomValues()
    {
        var controller = new CharacterController2D(
            new Vector2(1, 2),
            offset: new Vector2(0, 0.5f),
            gravityScale: 2.5f,
            stepHeight: 0.2f,
            layer: 3,
            collidesWith: 0b110
        );

        Assert.Equal(new Vector2(0, 0.5f), controller.Offset);
        Assert.Equal(2.5f, controller.GravityScale);
        Assert.Equal(0.2f, controller.StepHeight);
        Assert.Equal(3, controller.Layer);
        Assert.Equal(0b110u, controller.CollidesWith);
    }

    [Fact]
    public void Constructor_WithZeroWidth_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterController2D(0, 1));
    }

    [Fact]
    public void Constructor_WithZeroHeight_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterController2D(1, 0));
    }

    [Fact]
    public void Constructor_WithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CharacterController2D(new Vector2(-1, 1))
        );
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidStepHeight_ShouldThrow(float stepHeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CharacterController2D(new Vector2(1, 1), stepHeight: stepHeight)
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void Constructor_InvalidLayer_ShouldThrow(int layer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CharacterController2D(new Vector2(1, 1), layer: layer)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Constructor_BoundaryLayers_ShouldNotThrow(int layer)
    {
        var controller = new CharacterController2D(new Vector2(1, 1), layer: layer);
        Assert.Equal(layer, controller.Layer);
    }

    [Fact]
    public void Constructor_ZeroStepHeight_ShouldNotThrow()
    {
        var controller = new CharacterController2D(new Vector2(1, 1), stepHeight: 0f);
        Assert.Equal(0f, controller.StepHeight);
    }
}
