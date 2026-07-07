using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class TilemapTests
{
    private const string TexturePath = "Assets/tiles.png";

    private static Tileset MakeTileset(int columns = 4, int rows = 2) =>
        new(TexturePath, columns, rows);

    [Fact]
    public void Constructor_ShouldFillWithEmptyTiles()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 2);

        Assert.Equal(3, tilemap.Width);
        Assert.Equal(2, tilemap.Height);
        Assert.Equal(6, tilemap.Tiles.Length);
        Assert.All(tilemap.Tiles, tile => Assert.Equal(Tilemap.EmptyTile, tile));
    }

    [Fact]
    public void Constructor_Defaults_ShouldBeUnitTileSizeAndWhiteTint()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 1, height: 1);

        Assert.Equal(Vector2.One, tilemap.TileSize);
        Assert.Equal(Color.White, tilemap.Tint);
    }

    [Fact]
    public void Constructor_WithTiles_ShouldUseArrayDirectly()
    {
        var tiles = new[] { 0, 1, Tilemap.EmptyTile, 7 };

        var tilemap = new Tilemap(MakeTileset(), width: 2, height: 2, tiles);

        Assert.Same(tiles, tilemap.Tiles);
    }

    [Fact]
    public void Constructor_DefaultTileset_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Tilemap(default, width: 1, height: 1));
    }

    [Fact]
    public void Constructor_NonPositiveDimensions_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 0, height: 1)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 1, height: 0)
        );
    }

    [Fact]
    public void Constructor_NonPositiveTileSize_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 1, height: 1, tileSize: new Vector2(0f, 1f))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 1, height: 1, tileSize: new Vector2(1f, -1f))
        );
    }

    [Fact]
    public void Constructor_WidthTimesHeightOverflowingInt_ShouldThrow()
    {
        // 100000 * 100000 wraps int arithmetic; the constructor must reject it before
        // allocating or validating against a bogus product.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 100_000, height: 100_000)
        );
    }

    [Fact]
    public void Constructor_NonFiniteTileSize_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 1, height: 1, tileSize: new Vector2(float.NaN, 1f))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(
                MakeTileset(),
                width: 1,
                height: 1,
                tileSize: new Vector2(1f, float.PositiveInfinity)
            )
        );
    }

    [Fact]
    public void Constructor_WrongTileArrayLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new Tilemap(MakeTileset(), width: 2, height: 2, tiles: [0, 1, 2])
        );
    }

    [Fact]
    public void Constructor_TileIndexOutOfRange_ShouldThrow()
    {
        // Tileset is 4x2 = 8 tiles, so 8 is one past the end and -2 is below the sentinel.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 2, height: 1, tiles: [0, 8])
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Tilemap(MakeTileset(), width: 2, height: 1, tiles: [-2, 0])
        );
    }

    [Fact]
    public void GetTile_ShouldReadRowMajorFromTopRow()
    {
        // 3 wide, 2 high: row 0 (top) is [0, 1, 2], row 1 (bottom) is [3, 4, 5].
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 2, tiles: [0, 1, 2, 3, 4, 5]);

        Assert.Equal(0, tilemap.GetTile(column: 0, row: 0));
        Assert.Equal(2, tilemap.GetTile(column: 2, row: 0));
        Assert.Equal(3, tilemap.GetTile(column: 0, row: 1));
        Assert.Equal(5, tilemap.GetTile(column: 2, row: 1));
    }

    [Fact]
    public void SetTile_ShouldRoundTripThroughGetTile()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 3, height: 2);

        tilemap.SetTile(column: 1, row: 1, tileIndex: 5);

        Assert.Equal(5, tilemap.GetTile(column: 1, row: 1));
        Assert.Equal(Tilemap.EmptyTile, tilemap.GetTile(column: 0, row: 0));
    }

    [Fact]
    public void SetTile_EmptySentinel_ShouldClearCell()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 1, height: 1, tiles: [3]);

        tilemap.SetTile(column: 0, row: 0, Tilemap.EmptyTile);

        Assert.Equal(Tilemap.EmptyTile, tilemap.GetTile(column: 0, row: 0));
    }

    [Fact]
    public void SetTile_InvalidIndex_ShouldThrow()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 1, height: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.SetTile(0, 0, 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.SetTile(0, 0, -2));
    }

    [Fact]
    public void GetTile_OutOfBoundsCell_ShouldThrow()
    {
        var tilemap = new Tilemap(MakeTileset(), width: 2, height: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.GetTile(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.GetTile(2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.GetTile(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tilemap.GetTile(0, 2));
    }

    [Fact]
    public void SetTile_OnComponentCopy_ShouldBeVisibleThroughWorld()
    {
        // The tile array is shared between copies of the struct, so mutating a copy
        // retrieved from the world updates the stored component too (like Animation.Frames).
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Tilemap(MakeTileset(), width: 2, height: 2));

        var copy = world.GetComponent<Tilemap>(entity);
        copy.SetTile(column: 1, row: 0, tileIndex: 4);

        var reread = world.GetComponent<Tilemap>(entity);
        Assert.Equal(4, reread.GetTile(column: 1, row: 0));
    }
}
