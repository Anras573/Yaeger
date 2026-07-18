using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class BoxCollider2DTests
{
    [Fact]
    public void Constructor_WithSize_ShouldSetSizeAndDefaultOffset()
    {
        var collider = new BoxCollider2D(new Vector2(2, 3));

        Assert.Equal(new Vector2(2, 3), collider.Size);
        Assert.Equal(Vector2.Zero, collider.Offset);
    }

    [Fact]
    public void Constructor_WithSizeAndOffset_ShouldSetBoth()
    {
        var collider = new BoxCollider2D(new Vector2(2, 3), new Vector2(1, -1));

        Assert.Equal(new Vector2(2, 3), collider.Size);
        Assert.Equal(new Vector2(1, -1), collider.Offset);
    }

    [Fact]
    public void Constructor_WithWidthAndHeight_ShouldSetSize()
    {
        var collider = new BoxCollider2D(4, 5);

        Assert.Equal(new Vector2(4, 5), collider.Size);
        Assert.Equal(Vector2.Zero, collider.Offset);
    }

    [Fact]
    public void HalfSize_ShouldReturnHalfOfSize()
    {
        var collider = new BoxCollider2D(new Vector2(4, 6));

        Assert.Equal(new Vector2(2, 3), collider.HalfSize);
    }

    [Fact]
    public void Constructor_WithZeroWidth_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoxCollider2D(0, 5));
    }

    [Fact]
    public void Constructor_WithZeroHeight_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoxCollider2D(5, 0));
    }

    [Fact]
    public void Constructor_WithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoxCollider2D(new Vector2(-1, 2)));
    }

    [Fact]
    public void Constructor_WithNegativeHeight_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoxCollider2D(new Vector2(2, -1)));
    }

    [Fact]
    public void Constructor_ShouldDefaultToLayerZeroAllLayersNonTrigger()
    {
        var sized = new BoxCollider2D(new Vector2(2, 3));
        var widthHeight = new BoxCollider2D(4, 5);

        Assert.Equal(0, sized.Layer);
        Assert.Equal(BoxCollider2D.AllLayers, sized.CollidesWith);
        Assert.False(sized.IsTrigger);

        Assert.Equal(0, widthHeight.Layer);
        Assert.Equal(BoxCollider2D.AllLayers, widthHeight.CollidesWith);
        Assert.False(widthHeight.IsTrigger);
    }

    [Fact]
    public void Constructor_WithSize_ShouldSetLayerMaskAndTrigger()
    {
        var collider = new BoxCollider2D(
            new Vector2(2, 3),
            layer: 5,
            collidesWith: 0b1010,
            isTrigger: true
        );

        Assert.Equal(5, collider.Layer);
        Assert.Equal(0b1010u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void Constructor_WithWidthHeight_ShouldSetLayerMaskAndTrigger()
    {
        var collider = new BoxCollider2D(4, 5, layer: 3, collidesWith: 0b0110, isTrigger: true);

        Assert.Equal(3, collider.Layer);
        Assert.Equal(0b0110u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void Constructor_WithSize_InvalidLayer_ShouldThrow(int layer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BoxCollider2D(new Vector2(1, 1), layer: layer)
        );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void Constructor_WithWidthHeight_InvalidLayer_ShouldThrow(int layer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoxCollider2D(1, 1, layer: layer));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Constructor_BoundaryLayers_ShouldNotThrow(int layer)
    {
        var collider = new BoxCollider2D(1, 1, layer: layer);
        Assert.Equal(layer, collider.Layer);
    }
}
