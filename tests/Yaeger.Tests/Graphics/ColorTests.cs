using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ColorTests
{
    [Fact]
    public void Constructor_ShouldSetRGBValues()
    {
        // Arrange & Act
        var color = new Color(100, 150, 200);

        // Assert
        Assert.Equal(100, color.R);
        Assert.Equal(150, color.G);
        Assert.Equal(200, color.B);
    }

    [Fact]
    public void Constructor_ShouldSetAlphaValue()
    {
        // Arrange & Act
        var color = new Color(100, 150, 200, 128);

        // Assert
        Assert.Equal(128, color.A);
    }

    [Fact]
    public void Constructor_ShouldDefaultAlphaTo255()
    {
        // Arrange & Act
        var color = new Color(100, 150, 200);

        // Assert
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void White_ShouldBeFullyWhite()
    {
        // Arrange & Act
        var white = Color.White;

        // Assert
        Assert.Equal(255, white.R);
        Assert.Equal(255, white.G);
        Assert.Equal(255, white.B);
        Assert.Equal(255, white.A);
    }

    [Fact]
    public void Black_ShouldBeFullyBlack()
    {
        // Arrange & Act
        var black = Color.Black;

        // Assert
        Assert.Equal(0, black.R);
        Assert.Equal(0, black.G);
        Assert.Equal(0, black.B);
        Assert.Equal(255, black.A);
    }

    [Fact]
    public void Red_ShouldBeFullyRed()
    {
        // Arrange & Act
        var red = Color.Red;

        // Assert
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);
        Assert.Equal(255, red.A);
    }

    [Fact]
    public void Green_ShouldBeFullyGreen()
    {
        // Arrange & Act
        var green = Color.Green;

        // Assert
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);
        Assert.Equal(255, green.A);
    }

    [Fact]
    public void Blue_ShouldBeFullyBlue()
    {
        // Arrange & Act
        var blue = Color.Blue;

        // Assert
        Assert.Equal(0, blue.R);
        Assert.Equal(0, blue.G);
        Assert.Equal(255, blue.B);
        Assert.Equal(255, blue.A);
    }

    [Fact]
    public void Color_ShouldBeValueType()
    {
        // Arrange
        var color1 = new Color(100, 150, 200);

        // Act
        var color2 = color1;

        // Assert
        Assert.Equal(color1.R, color2.R);
        Assert.Equal(color1.G, color2.G);
        Assert.Equal(color1.B, color2.B);
        Assert.Equal(color1.A, color2.A);
    }

    [Fact]
    public void Color_ShouldSupportTransparency()
    {
        // Arrange & Act
        var transparentRed = new Color(255, 0, 0, 128);

        // Assert
        Assert.Equal(255, transparentRed.R);
        Assert.Equal(128, transparentRed.A);
    }

    [Fact]
    public void Color_ShouldHandleZeroAlpha()
    {
        // Arrange & Act
        var fullyTransparent = new Color(100, 150, 200, 0);

        // Assert
        Assert.Equal(0, fullyTransparent.A);
    }
}