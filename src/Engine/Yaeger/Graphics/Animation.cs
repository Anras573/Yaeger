namespace Yaeger.Graphics;

/// <summary>
/// Represents a frame in an animation with a texture path and duration.
/// </summary>
public readonly struct AnimationFrame(string texturePath, float duration)
{
    /// <summary>
    /// Gets the path to the texture for this frame.
    /// </summary>
    public string TexturePath { get; } = texturePath;

    /// <summary>
    /// Gets the duration in seconds that this frame should be displayed.
    /// </summary>
    public float Duration { get; } = duration;
}

/// <summary>
/// Component that defines an animation as a collection of frames.
/// Each frame consists of a texture and a duration.
/// </summary>
public readonly struct Animation
{
    /// <summary>
    /// Gets the frames that make up this animation.
    /// </summary>
    public AnimationFrame[] Frames { get; }

    /// <summary>
    /// Gets whether the animation should loop when it reaches the end.
    /// </summary>
    public bool Loop { get; }

    /// <summary>
    /// Initializes a new instance of the Animation struct.
    /// </summary>
    /// <param name="frames">The frames that make up the animation.</param>
    /// <param name="loop">Whether the animation should loop. Default is true.</param>
    public Animation(AnimationFrame[] frames, bool loop = true)
    {
        Frames = frames;
        Loop = loop;
    }

    /// <summary>
    /// Gets the total duration of the animation in seconds.
    /// </summary>
    public float TotalDuration
    {
        get
        {
            float total = 0;
            foreach (var frame in Frames)
            {
                total += frame.Duration;
            }
            return total;
        }
    }
}