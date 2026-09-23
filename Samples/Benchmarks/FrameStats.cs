namespace Benchmarks;

/// <summary>
/// Shared once-per-second console reporter used by every benchmark: accumulates frames and
/// elapsed time via <see cref="Tick"/> and prints FPS plus average frame time, in the same format
/// for all three benchmarks, once a second has elapsed.
/// </summary>
public sealed class FrameStats(string label)
{
    private int _frameCount;
    private double _secondElapsed;

    /// <param name="deltaTime">This frame's delta time, in seconds.</param>
    /// <param name="extra">
    /// Optional extra fields appended to the line (e.g. draw-call counts) — only evaluated when a
    /// report is about to be printed.
    /// </param>
    public void Tick(double deltaTime, Func<string>? extra = null)
    {
        _frameCount++;
        _secondElapsed += deltaTime;
        if (_secondElapsed < 1.0)
        {
            return;
        }

        var averageFrameMs = _secondElapsed * 1000.0 / _frameCount;
        var suffix = extra is null ? string.Empty : $" | {extra()}";
        Console.WriteLine(
            $"[{label}] FPS: {_frameCount} | avg frame: {averageFrameMs:F2} ms{suffix}"
        );

        _frameCount = 0;
        _secondElapsed = 0;
    }
}
