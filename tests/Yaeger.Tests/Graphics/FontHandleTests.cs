using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class FontHandleTests
{
    [Fact]
    public void Constructor_WithValidId_ShouldStoreId()
    {
        var handle = new FontHandle("fonts/arial.ttf");

        Assert.Equal("fonts/arial.ttf", handle.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidId_ShouldThrowArgumentException(string? id)
    {
        Assert.Throws<ArgumentException>(() => new FontHandle(id!));
    }

    [Fact]
    public void Id_WhenDefaultInitialized_ShouldThrowInvalidOperationException()
    {
        var handle = default(FontHandle);
        Assert.Throws<InvalidOperationException>(() => _ = handle.Id);
    }
}
