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
}
