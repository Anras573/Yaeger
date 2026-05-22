namespace Yaeger.Platform;

/// <summary>
/// Mutable native time source that can be advanced by the host game loop.
/// </summary>
public sealed class FrameTimeSource : ITimeSource
{
    public float DeltaTime { get; private set; }

    public double TotalTime { get; private set; }

    public void Advance(double deltaTimeSeconds)
    {
        if (deltaTimeSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaTimeSeconds),
                "Delta time cannot be negative."
            );
        }

        DeltaTime = (float)deltaTimeSeconds;
        TotalTime += deltaTimeSeconds;
    }
}
