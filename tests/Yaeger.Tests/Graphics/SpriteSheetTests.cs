using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SpriteSheetTests
{
    [Fact]
    public void GetFrameUv_SingleRowSheet_MapsLeftToRight()
    {
        // Arrange
        var sheet = new SpriteSheet("sheet.png", columns: 4);

        // Act
        var (uvMin, uvMax) = sheet.GetFrameUv(1);

        // Assert
        Assert.Equal(0.25f, uvMin.X, 5);
        Assert.Equal(0f, uvMin.Y, 5);
        Assert.Equal(0.5f, uvMax.X, 5);
        Assert.Equal(1f, uvMax.Y, 5);
    }

    [Fact]
    public void GetFrameUv_SingleColumnSheet_MapsTopToBottom()
    {
        // Arrange
        var sheet = new SpriteSheet("sheet.png", columns: 1, rows: 3);

        // Act
        var (firstUvMin, firstUvMax) = sheet.GetFrameUv(0);
        var (lastUvMin, lastUvMax) = sheet.GetFrameUv(2);

        // Assert
        Assert.Equal(2f / 3f, firstUvMin.Y, 5);
        Assert.Equal(1f, firstUvMax.Y, 5);
        Assert.Equal(0f, lastUvMin.Y, 5);
        Assert.Equal(1f / 3f, lastUvMax.Y, 5);
    }

    [Fact]
    public void GetFrameUv_MultiRowSheet_UsesTopToBottomFrameIndexing()
    {
        // Arrange
        var sheet = new SpriteSheet("sheet.png", columns: 3, rows: 2);

        // Act
        var (uvMin, uvMax) = sheet.GetFrameUv(3); // row 1, column 0

        // Assert
        Assert.Equal(0f, uvMin.X, 5);
        Assert.Equal(0f, uvMin.Y, 5);
        Assert.Equal(1f / 3f, uvMax.X, 5);
        Assert.Equal(0.5f, uvMax.Y, 5);
    }

    [Fact]
    public void GetFrameUv_InvalidFrameIndex_Throws()
    {
        // Arrange
        var sheet = new SpriteSheet("sheet.png", columns: 2, rows: 2);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetFrameUv(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetFrameUv(4));
    }
}
