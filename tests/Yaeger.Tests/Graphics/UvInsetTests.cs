using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class UvInsetTests
{
    [Fact]
    public void GetFrameUv_WithTextureSize_InsetsByHalfTexel()
    {
        var sheet = new SpriteSheet("a.png", 2, 2);
        var size = new Vector2(128f, 64f);

        var (exactMin, exactMax) = sheet.GetFrameUv(1);
        var (uvMin, uvMax) = sheet.GetFrameUv(1, size);

        Assert.Equal(exactMin.X + 0.5f / 128f, uvMin.X, 6);
        Assert.Equal(exactMin.Y + 0.5f / 64f, uvMin.Y, 6);
        Assert.Equal(exactMax.X - 0.5f / 128f, uvMax.X, 6);
        Assert.Equal(exactMax.Y - 0.5f / 64f, uvMax.Y, 6);
    }

    [Fact]
    public void GetFrameUv_UnknownTextureSize_ReturnsExactEdges()
    {
        var sheet = new SpriteSheet("a.png", 2, 2);

        Assert.Equal(sheet.GetFrameUv(2), sheet.GetFrameUv(2, Vector2.Zero));
    }

    [Fact]
    public void GetTileUv_WithTextureSize_MatchesSpriteSheet()
    {
        var size = new Vector2(64f, 64f);

        Assert.Equal(
            new SpriteSheet("a.png", 4, 4).GetFrameUv(5, size, 1f),
            new Tileset("a.png", 4, 4).GetTileUv(5, size, 1f)
        );
    }
}
