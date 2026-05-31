using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ParallaxLayerTests
{
    [Fact]
    public void Constructor_ShouldApplyDefaultScrollFactors()
    {
        var layer = new ParallaxLayer();

        Assert.Equal(0.5f, layer.ScrollFactorX);
        Assert.Equal(0f, layer.ScrollFactorY);
    }

    [Fact]
    public void Constructor_ShouldSetScrollFactors()
    {
        var layer = new ParallaxLayer(scrollFactorX: 0.2f, scrollFactorY: 0.8f);

        Assert.Equal(0.2f, layer.ScrollFactorX);
        Assert.Equal(0.8f, layer.ScrollFactorY);
    }

    [Fact]
    public void BasePosition_ShouldDefaultToZero()
    {
        var layer = new ParallaxLayer();

        Assert.Equal(Vector2.Zero, layer.BasePosition);
    }

    [Fact]
    public void BasePosition_ShouldBeSettable()
    {
        var layer = new ParallaxLayer { BasePosition = new Vector2(100f, 50f) };

        Assert.Equal(new Vector2(100f, 50f), layer.BasePosition);
    }
}
