using Silk.NET.Windowing;
using Yaeger.Windowing;

namespace Yaeger.Tests.Windowing;

public class WindowSettingsTests
{
    [Fact]
    public void ToWindowOptions_ShouldApplyDefaults()
    {
        var options = new WindowSettings().ToWindowOptions();

        Assert.Equal("Yaeger", options.Title);
        Assert.Equal(1280, options.Size.X);
        Assert.Equal(720, options.Size.Y);
        Assert.True(options.VSync);
        Assert.Equal(WindowBorder.Resizable, options.WindowBorder);
        Assert.Equal(WindowState.Normal, options.WindowState);
    }

    [Fact]
    public void ToWindowOptions_ShouldMapCustomValues()
    {
        var settings = new WindowSettings
        {
            Title = "My Game",
            Width = 800,
            Height = 600,
            VSync = false,
            Resizable = false,
            Fullscreen = true,
            UpdatesPerSecond = 60,
            FramesPerSecond = 144,
        };

        var options = settings.ToWindowOptions();

        Assert.Equal("My Game", options.Title);
        Assert.Equal(800, options.Size.X);
        Assert.Equal(600, options.Size.Y);
        Assert.False(options.VSync);
        Assert.Equal(WindowBorder.Fixed, options.WindowBorder);
        Assert.Equal(WindowState.Fullscreen, options.WindowState);
        Assert.Equal(60, options.UpdatesPerSecond);
        Assert.Equal(144, options.FramesPerSecond);
    }

    [Fact]
    public void ToWindowOptions_ShouldKeepDefaultRates_WhenUnset()
    {
        var options = new WindowSettings().ToWindowOptions();

        Assert.Equal(WindowOptions.Default.UpdatesPerSecond, options.UpdatesPerSecond);
        Assert.Equal(WindowOptions.Default.FramesPerSecond, options.FramesPerSecond);
    }
}
