namespace Yaeger.Graphics;

/// <summary>
/// ECS component describing playback state for a skinned mesh's skeletal animation. The
/// <see cref="Yaeger.Systems.SkeletalAnimationSystem"/> advances <see cref="Time"/> each frame and
/// samples <see cref="CurrentClip"/> from the entity's <see cref="SkeletonHandle"/>.
/// </summary>
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

    public AnimationPlayer(string? currentClip, bool loop = true, float speed = 1f)
    {
        CurrentClip = currentClip;
        Time = 0f;
        Loop = loop;
        Speed = speed;
    }
}
