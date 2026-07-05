using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class TilesetTests
{
    private const string TexturePath = "Assets/tiles.png";

    [Fact]
    public void Constructor_ShouldExposeGridProperties()
    {
        var tileset = new Tileset(TexturePath, columns: 4, rows: 2);

        Assert.Equal(TexturePath, tileset.TexturePath);
        Assert.Equal(4, tileset.Columns);
        Assert.Equal(2, tileset.Rows);
        Assert.Equal(8, tileset.TileCount);
    }

    [Fact]
    public void Constructor_RowsDefault_ShouldBeOne()
    {
        var tileset = new Tileset(TexturePath, columns: 3);

        Assert.Equal(1, tileset.Rows);
        Assert.Equal(3, tileset.TileCount);
    }

    [Fact]
    public void Constructor_EmptyTexturePath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Tileset("", columns: 1));
    }

    [Fact]
    public void Constructor_NonPositiveColumns_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tileset(TexturePath, columns: 0));
    }

    [Fact]
    public void Default_ShouldHaveZeroTileCount()
    {
        var tileset = default(Tileset);

        Assert.Equal(0, tileset.TileCount);
    }

    [Fact]
    public void GetTileUv_FirstTile_ShouldBeTopLeft()
    {
        var tileset = new Tileset(TexturePath, columns: 2, rows: 2);

        var (uvMin, uvMax) = tileset.GetTileUv(0);

        Assert.Equal(0f, uvMin.X, 0.0001f);
        Assert.Equal(0.5f, uvMin.Y, 0.0001f);
        Assert.Equal(0.5f, uvMax.X, 0.0001f);
        Assert.Equal(1f, uvMax.Y, 0.0001f);
    }

    [Fact]
    public void GetTileUv_LastTile_ShouldBeBottomRight()
    {
        var tileset = new Tileset(TexturePath, columns: 2, rows: 2);

        var (uvMin, uvMax) = tileset.GetTileUv(3);

        Assert.Equal(0.5f, uvMin.X, 0.0001f);
        Assert.Equal(0f, uvMin.Y, 0.0001f);
        Assert.Equal(1f, uvMax.X, 0.0001f);
        Assert.Equal(0.5f, uvMax.Y, 0.0001f);
    }

    [Fact]
    public void GetTileUv_ShouldMatchSpriteSheetFrameUv()
    {
        var tileset = new Tileset(TexturePath, columns: 4, rows: 3);
        var sheet = new SpriteSheet(TexturePath, columns: 4, rows: 3);

        for (var i = 0; i < tileset.TileCount; i++)
        {
            Assert.Equal(sheet.GetFrameUv(i), tileset.GetTileUv(i));
        }
    }

    [Fact]
    public void GetTileUv_OutOfRangeIndex_ShouldThrow()
    {
        var tileset = new Tileset(TexturePath, columns: 2, rows: 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => tileset.GetTileUv(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tileset.GetTileUv(4));
    }
}
