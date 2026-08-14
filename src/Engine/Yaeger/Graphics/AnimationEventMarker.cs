namespace Yaeger.Graphics;

/// <summary>
/// A named moment authored at a specific time (seconds) within an <see cref="AnimationClip"/> — a
/// footstep, a muzzle flash, a sword-swing whoosh. <see cref="Systems.SkeletalAnimationSystem"/>
/// raises an <see cref="AnimationEvent"/> as playback crosses one; see docs/skeletal-animation.md.
/// </summary>
public readonly record struct AnimationEventMarker(float Time, string Key);
