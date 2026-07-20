namespace Yaeger.Graphics;

/// <summary>
/// ECS component describing playback state for a skinned mesh's skeletal animation. The
/// <see cref="Yaeger.Systems.SkeletalAnimationSystem"/> advances <see cref="Time"/> each frame and
/// samples <see cref="CurrentClip"/> from the entity's <see cref="SkeletonHandle"/>.
/// </summary>
/// <remarks>
/// <see cref="PreviousClip"/>/<see cref="PreviousTime"/>/<see cref="FadeDuration"/>/
/// <see cref="FadeElapsed"/> hold an in-progress crossfade, set by
/// <see cref="Yaeger.Systems.SkeletalAnimationSystem.CrossFadeTo"/> rather than by hand. Plain
/// struct fields keep the component serializable-friendly, matching every other field here.
/// </remarks>
public struct AnimationPlayer
{
    /// <summary>Name of the clip to play, or <c>null</c> to hold the bind pose.</summary>
    public string? CurrentClip;

    /// <summary>Playback position in seconds.</summary>
    public float Time;

    /// <summary>When <c>true</c>, playback wraps around the clip duration; otherwise it clamps to the end.</summary>
    public bool Loop;

    /// <summary>
    /// Playback speed multiplier applied to delta time (1 = real time). A negative value plays the
    /// clip in reverse; when looping, time wraps around the clip duration.
    /// </summary>
    public float Speed;

    /// <summary>
    /// Name of the clip being faded out of, or <c>null</c> when no crossfade is in progress (or the
    /// fade-out source is the bind pose).
    /// </summary>
    public string? PreviousClip;

    /// <summary>Playback position of <see cref="PreviousClip"/>, in seconds.</summary>
    public float PreviousTime;

    /// <summary>Total crossfade length in seconds. Zero means no crossfade is in progress.</summary>
    public float FadeDuration;

    /// <summary>Seconds elapsed since the crossfade began.</summary>
    public float FadeElapsed;

    public AnimationPlayer(string? currentClip, bool loop = true, float speed = 1f)
    {
        CurrentClip = currentClip;
        Time = 0f;
        Loop = loop;
        Speed = speed;
        PreviousClip = null;
        PreviousTime = 0f;
        FadeDuration = 0f;
        FadeElapsed = 0f;
    }
}
