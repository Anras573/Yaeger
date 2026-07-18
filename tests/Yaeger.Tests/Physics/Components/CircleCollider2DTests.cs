using System.Numerics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Components;

public class CircleCollider2DTests
{
    [Fact]
    public void Constructor_WithRadius_ShouldSetRadiusAndDefaultOffset()
    {
        var collider = new CircleCollider2D(5.0f);

        Assert.Equal(5.0f, collider.Radius);
        Assert.Equal(Vector2.Zero, collider.Offset);
    }

    [Fact]
    public void Constructor_WithRadiusAndOffset_ShouldSetBoth()
    {
        var collider = new CircleCollider2D(3.0f, new Vector2(1, 2));

        Assert.Equal(3.0f, collider.Radius);
        Assert.Equal(new Vector2(1, 2), collider.Offset);
    }

    [Fact]
    public void Constructor_WithZeroRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleCollider2D(0.0f));
    }

    [Fact]
    public void Constructor_WithNegativeRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleCollider2D(-1.0f));
    }

    [Fact]
    public void Constructor_ShouldDefaultToLayerZeroAllLayersNonTrigger()
    {
        var collider = new CircleCollider2D(5.0f);

        Assert.Equal(0, collider.Layer);
        Assert.Equal(CircleCollider2D.AllLayers, collider.CollidesWith);
        Assert.False(collider.IsTrigger);
    }

    [Fact]
    public void Constructor_ShouldSetLayerMaskAndTrigger()
    {
        var collider = new CircleCollider2D(5.0f, layer: 5, collidesWith: 0b1010, isTrigger: true);

        Assert.Equal(5, collider.Layer);
        Assert.Equal(0b1010u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void Constructor_InvalidLayer_ShouldThrow(int layer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircleCollider2D(1f, layer: layer));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Constructor_BoundaryLayers_ShouldNotThrow(int layer)
    {
        var collider = new CircleCollider2D(1f, layer: layer);
        Assert.Equal(layer, collider.Layer);
    }
}
