using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Yaeger.Windowing;

/// <summary>
/// Yaeger-owned window configuration passed to <see cref="Window.Create(WindowSettings?)"/>,
/// so game code never touches Silk.NET types.
/// </summary>
public sealed record WindowSettings
{
    /// <summary>Window title bar text.</summary>
    public string Title { get; init; } = "Yaeger";

    /// <summary>Initial client width in pixels.</summary>
    public int Width { get; init; } = 1280;

    /// <summary>Initial client height in pixels.</summary>
    public int Height { get; init; } = 720;

    /// <summary>Whether presentation is synchronised to the display refresh.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>Whether the user can resize the window.</summary>
    public bool Resizable { get; init; } = true;

    /// <summary>Whether the window starts in fullscreen.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Update-event rate in Hz; <c>null</c> keeps Silk.NET's default.</summary>
    public double? UpdatesPerSecond { get; init; }

    /// <summary>Render-event rate in Hz; <c>null</c> keeps Silk.NET's default.</summary>
    public double? FramesPerSecond { get; init; }

    /// <summary>
    /// Whether to initialise audio. Set <c>false</c> to skip it entirely (headless smoke tests,
    /// tools). Even when <c>true</c>, a missing audio device is non-fatal:
    /// <see cref="Window.AudioContext"/> is simply <c>null</c>.
    /// </summary>
    public bool EnableAudio { get; init; } = true;

    internal WindowOptions ToWindowOptions()
    {
        var options = WindowOptions.Default;
        options.Title = Title;
        options.Size = new Vector2D<int>(Width, Height);
        options.VSync = VSync;
        options.WindowBorder = Resizable ? WindowBorder.Resizable : WindowBorder.Fixed;
        options.WindowState = Fullscreen ? WindowState.Fullscreen : WindowState.Normal;
        if (UpdatesPerSecond is { } ups)
            options.UpdatesPerSecond = ups;
        if (FramesPerSecond is { } fps)
            options.FramesPerSecond = fps;
        return options;
    }
}
