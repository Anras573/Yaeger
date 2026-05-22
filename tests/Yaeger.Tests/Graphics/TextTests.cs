using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class TextTests
{
    [Fact]
    public void Constructor_WithFontHandle_DoesNotExposeNativeFont()
    {
        var font = new FontHandle("Assets/Fonts/Test.ttf");

        var text = new Text("Hello", font, 24, Color.White);

        Assert.Equal(font, text.FontHandle);
        Assert.False(text.TryGetNativeFont(out _));
    }

    [Fact]
    public void Constructor_WithIFontHandle_StoresEquivalentFontHandle()
    {
        var runtimeHandle = new TestFontHandle("Assets/Fonts/Test.ttf");

        var text = new Text("Hello", runtimeHandle, 24, Color.White);

        Assert.Equal(new FontHandle(runtimeHandle.Id), text.FontHandle);
        Assert.False(text.TryGetNativeFont(out _));
    }

    [Fact]
    public void Deconstruct_WithFontHandle_DoesNotThrow()
    {
        var expectedFont = new FontHandle("Assets/Fonts/Test.ttf");
        var text = new Text("Hello", expectedFont, 24, Color.White);

        var (content, font, fontSize, color) = text;

        Assert.Equal("Hello", content);
        Assert.Equal(expectedFont, font);
        Assert.Equal(24, fontSize);
        Assert.Equal(Color.White, color);
    }

    private sealed class TestFontHandle(string id) : IFontHandle
    {
        public string Id { get; } = id;
    }
}
