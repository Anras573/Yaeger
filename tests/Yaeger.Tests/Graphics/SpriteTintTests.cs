using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SpriteTintTests
{
    [Fact]
    public void Sprite_DefaultTint_IsWhite()
    {
        // Arrange & Act
        var sprite = new Sprite("test.png");

        // Assert
        Assert.Equal(255, sprite.Tint.R);
        Assert.Equal(255, sprite.Tint.G);
        Assert.Equal(255, sprite.Tint.B);
        Assert.Equal(255, sprite.Tint.A);
    }

    [Fact]
    public void Sprite_WithTint_PreservesTintColor()
    {
        // Arrange
        var redTint = new Color(255, 0, 0, 128);

        // Act
        var sprite = new Sprite("test.png", redTint);

        // Assert
        Assert.Equal(255, sprite.Tint.R);
        Assert.Equal(0, sprite.Tint.G);
        Assert.Equal(0, sprite.Tint.B);
        Assert.Equal(128, sprite.Tint.A);
    }

    [Fact]
    public void Sprite_WithNullTint_DefaultsToWhite()
    {
        // Arrange & Act
        var sprite = new Sprite("test.png", null);

        // Assert
        Assert.Equal(Color.White.R, sprite.Tint.R);
        Assert.Equal(Color.White.G, sprite.Tint.G);
        Assert.Equal(Color.White.B, sprite.Tint.B);
        Assert.Equal(Color.White.A, sprite.Tint.A);
    }
}
