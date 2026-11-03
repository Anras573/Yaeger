using SkiaSharp;

namespace Yaeger.Tests.Font;

public class SkiaNativeTests
{
    [Fact]
    public void SKTypeface_FromFontBytes_ShouldLoadNativeSkia()
    {
        // Arrange — mirrors what GlyphAtlas does on construction; regression for #333, where
        // libSkiaSharp.so was missing on Linux and SKData's type initializer threw.
        var bytes = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "TestAssets", "Roboto-Regular.ttf")
        );

        // Act
        using var data = SKData.CreateCopy(bytes);
        using var typeface = SKTypeface.FromData(data);
        using var font = new SKFont(typeface, 24);

        // Assert
        Assert.NotNull(typeface);
        Assert.True(font.MeasureText("Hello") > 0);
    }
}
