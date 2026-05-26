using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="ITimeSource"/> driven by the <c>DOMHighResTimeStamp</c> supplied by
/// <c>requestAnimationFrame</c>.  Call <see cref="Advance"/> at the start of each game tick.
/// </summary>
public sealed class BrowserTimeSource : ITimeSource
{
    private double _lastTimestampMs;

    public float DeltaTime { get; private set; }

    public double TotalTime { get; private set; }

    /// <summary>
    /// Advances the time source using a <c>requestAnimationFrame</c> timestamp in milliseconds.
    /// On the very first tick, <see cref="DeltaTime"/> is treated as zero to avoid a large
    /// initial jump.
    /// </summary>
    public void Advance(double timestampMs)
    {
        var deltaMs = _lastTimestampMs > 0 ? timestampMs - _lastTimestampMs : 0.0;
        _lastTimestampMs = timestampMs;
        DeltaTime = (float)(deltaMs / 1000.0);
        TotalTime += deltaMs / 1000.0;
    }
}
