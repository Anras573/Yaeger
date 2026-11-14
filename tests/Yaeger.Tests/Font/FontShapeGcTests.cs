namespace Yaeger.Tests.Font;

public class FontShapeGcTests
{
    [Fact]
    public void Shape_ShouldSurviveGarbageCollection()
    {
        // Arrange
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Samples",
            "Platformer",
            "Assets",
            "Roboto-Regular.ttf"
        );
        var bytes = File.ReadAllBytes(path);
        using var font = new Yaeger.Font.Font("gc-test", bytes);

        // Act
        GlyphCount = 0;
        for (var i = 0; i < 300; i++)
        {
            GlyphCount += font.Shape("Shift gravity to fall into the glowing chute.", 20).Length;
            if (i % 50 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Assert
        Assert.True(GlyphCount > 0);
    }

    private int GlyphCount;
}
