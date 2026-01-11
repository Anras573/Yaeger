using System;

namespace Yaeger.Graphics;

/// <summary>
/// Represents a frame in an animation with a texture path and duration.
/// </summary>
public readonly record struct AnimationFrame
{
    /// <summary>
    /// Gets the texture path for this frame.
    /// </summary>
    public string TexturePath { get; init; }

    /// <summary>
    /// Gets the duration of this frame in seconds.
    /// Must be greater than 0.
    /// </summary>
    public float Duration { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationFrame"/> struct.
    /// </summary>
    /// <param name="texturePath">The texture path for this frame.</param>
    /// <param name="duration">The duration of this frame in seconds. Must be greater than 0.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="texturePath"/> is null or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="duration"/> is less than or equal to 0.</exception>
    public AnimationFrame(string texturePath, float duration)
    {
        ArgumentException.ThrowIfNullOrEmpty(texturePath);
        if (duration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than 0.");
        }

        TexturePath = texturePath;
        Duration = duration;
    }
}
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