using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class CameraBoundsTests
{
    [Fact]
    public void Constructor_ShouldSetMinAndMax()
    {
        var bounds = new CameraBounds(new Vector2(-5, -5), new Vector2(10, 20));

        Assert.Equal(new Vector2(-5, -5), bounds.Min);
        Assert.Equal(new Vector2(10, 20), bounds.Max);
    }

    [Theory]
    [InlineData(11, 0, 10, 10)] // min.X > max.X
    [InlineData(0, 11, 10, 10)] // min.Y > max.Y
    public void Constructor_MinExceedsMax_ShouldThrow(
        float minX,
        float minY,
        float maxX,
        float maxY
    )
    {
        Assert.Throws<ArgumentException>(() =>
            new CameraBounds(new Vector2(minX, minY), new Vector2(maxX, maxY))
        );
    }

    [Fact]
    public void Constructor_MinEqualsMax_ShouldNotThrow()
    {
        var bounds = new CameraBounds(new Vector2(5, 5), new Vector2(5, 5));

        Assert.Equal(new Vector2(5, 5), bounds.Min);
        Assert.Equal(new Vector2(5, 5), bounds.Max);
    }

    [Fact]
    public void FromTilemap_ShouldSpanTheFullMapFromItsBottomLeft()
    {
        var tileset = new Tileset("Assets/tiles.png", columns: 1);
        var tilemap = new Tilemap(tileset, width: 20, height: 10, tileSize: new Vector2(2, 3));
        var transform = new Transform2D(new Vector2(-1, -2));

        var bounds = CameraBounds.FromTilemap(tilemap, transform);

        Assert.Equal(new Vector2(-1, -2), bounds.Min);
        // 20 tiles * 2 units wide = 40; 10 tiles * 3 units tall = 30, from the bottom-left origin.
        Assert.Equal(new Vector2(39, 28), bounds.Max);
    }
}
