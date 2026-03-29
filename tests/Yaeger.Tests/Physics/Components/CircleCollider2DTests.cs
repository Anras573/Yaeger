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
}
