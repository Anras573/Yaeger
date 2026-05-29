using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="ITimeSource"/> driven by the <c>DOMHighResTimeStamp</c> supplied by
/// <c>requestAnimationFrame</c>.  Call <see cref="Advance"/> at the start of each game tick.
/// </summary>
public sealed class BrowserTimeSource : ITimeSource
{
    public const float DefaultMaxDeltaTimeSeconds = 1f / 15f;

    private double _lastTimestampMs;
    private bool _initialized;
    private readonly float _maxDeltaTimeSeconds;

    public BrowserTimeSource(float maxDeltaTimeSeconds = DefaultMaxDeltaTimeSeconds)
    {
        if (maxDeltaTimeSeconds <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(maxDeltaTimeSeconds),
                maxDeltaTimeSeconds,
                "Maximum delta time must be greater than zero."
            );

        _maxDeltaTimeSeconds = maxDeltaTimeSeconds;
    }

    public float DeltaTime { get; private set; }

    public double TotalTime { get; private set; }

    /// <summary>
    /// Advances the time source using a <c>requestAnimationFrame</c> timestamp in milliseconds.
    /// On the very first tick, <see cref="DeltaTime"/> is set to zero to avoid a large initial
    /// jump regardless of the timestamp value (including zero, which is valid on some browsers).
    /// Subsequent ticks compute the delta from the previous timestamp. Backward timestamps
    /// (e.g. caused by a non-monotonic browser clock or bad caller input) are treated as
    /// a no-op so that the baseline is not moved backward and the next valid frame does not
    /// over-report <see cref="DeltaTime"/>. Large frame gaps are clamped to
    /// <see cref="_maxDeltaTimeSeconds"/> to avoid simulation spikes after background tab stalls.
    /// This also ensures <see cref="ITimeSource.TotalTime"/> never decreases.
    /// No exception is thrown because throwing inside a <c>requestAnimationFrame</c> callback
    /// chain would silently stop the loop rather than degrade gracefully.
    /// </summary>
    public void Advance(double timestampMs)
    {
        if (!_initialized)
        {
            _initialized = true;
            _lastTimestampMs = timestampMs;
            DeltaTime = 0.0f;
            return;
        }

        var clampedTimestampMs = Math.Max(timestampMs, _lastTimestampMs);
        var deltaMs = clampedTimestampMs - _lastTimestampMs;
        _lastTimestampMs = clampedTimestampMs;
        var deltaSeconds = Math.Min(deltaMs / 1000.0, _maxDeltaTimeSeconds);
        DeltaTime = (float)deltaSeconds;
        TotalTime += deltaSeconds;
    }
}
