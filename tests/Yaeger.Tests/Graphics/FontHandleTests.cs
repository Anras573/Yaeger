using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class FontHandleTests
{
    [Fact]
    public void Constructor_WithValidId_ShouldStoreId()
    {
        // Arrange & Act
        var handle = new FontHandle("fonts/arial.ttf");

        // Assert
        Assert.Equal("fonts/arial.ttf", handle.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithWhitespaceOrEmptyId_ShouldThrowArgumentException(string id)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => new FontHandle(id));
    }

    [Fact]
    public void Id_WhenDefaultInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var handle = default(FontHandle);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _ = handle.Id);
    }
}
