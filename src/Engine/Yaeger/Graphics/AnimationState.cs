namespace Yaeger.Graphics;

/// <summary>
/// Component that tracks the current state of an animation.
/// This includes the current frame index and elapsed time within that frame.
/// </summary>
public record struct AnimationState(int CurrentFrameIndex = 0, float ElapsedTime = 0f, bool IsFinished = false)
{
    /// <summary>
    /// Gets or sets the index of the current frame being displayed.
    /// </summary>
    public int CurrentFrameIndex { get; set; } = CurrentFrameIndex;

    /// <summary>
    /// Gets or sets the time elapsed since the current frame started, in seconds.
    /// </summary>
    public float ElapsedTime { get; set; } = ElapsedTime;

    /// <summary>
    /// Gets or sets whether the animation has finished playing (only relevant for non-looping animations).
    /// </summary>
    public bool IsFinished { get; set; } = IsFinished;
}