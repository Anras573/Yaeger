namespace Yaeger.Graphics;

/// <summary>
/// Represents a frame in an animation with a texture path and duration.
/// </summary>
public readonly record struct AnimationFrame(string TexturePath, float Duration);

/// <summary>
/// Component that defines an animation as a collection of frames.
/// Each frame consists of a texture and a duration.
/// </summary>
public readonly record struct Animation
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
    /// <exception cref="ArgumentNullException">Thrown when frames is null.</exception>
    /// <exception cref="ArgumentException">Thrown when frames is empty.</exception>
    public Animation(AnimationFrame[] frames, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Length == 0)
        {
            throw new ArgumentException("Animation must have at least one frame.", nameof(frames));
        }

        Frames = frames;
        Loop = loop;
    }

    /// <summary>
    /// Gets the total duration of the animation in seconds.
    /// </summary>
    public float TotalDuration => Frames.Sum(frame => frame.Duration);
}