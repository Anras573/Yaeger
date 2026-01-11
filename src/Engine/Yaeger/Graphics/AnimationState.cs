namespace Yaeger.Graphics;

/// <summary>
/// Component that tracks the current state of an animation.
/// This includes the current frame index and elapsed time within that frame.
/// </summary>
public struct AnimationState
{
    /// <summary>
    /// Gets or sets the index of the current frame being displayed.
    /// </summary>
    public int CurrentFrameIndex { get; set; }

    /// <summary>
    /// Gets or sets the time elapsed since the current frame started, in seconds.
    /// </summary>
    public float ElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets whether the animation has finished playing (only relevant for non-looping animations).
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// Initializes a new instance of the AnimationState struct.
    /// </summary>
    /// <param name="currentFrameIndex">The index of the current frame. Default is 0.</param>
    /// <param name="elapsedTime">The elapsed time. Default is 0.</param>
    /// <param name="isFinished">Whether the animation is finished. Default is false.</param>
    public AnimationState(int currentFrameIndex = 0, float elapsedTime = 0f, bool isFinished = false)
    {
        CurrentFrameIndex = currentFrameIndex;
        ElapsedTime = elapsedTime;
        IsFinished = isFinished;
    }
}