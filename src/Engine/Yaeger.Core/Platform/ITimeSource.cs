namespace Yaeger.Platform;

/// <summary>
/// Time abstraction for update-driven systems.
/// </summary>
public interface ITimeSource
{
    float DeltaTime { get; }
    double TotalTime { get; }
}
