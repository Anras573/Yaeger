using Yaeger.ECS;

namespace Yaeger.Graphics;

/// <summary>
/// Raised by <see cref="Systems.SkeletalAnimationSystem.OnAnimationEvent"/> when <paramref name="Entity"/>'s
/// playback crosses an <see cref="AnimationEventMarker"/> authored on <paramref name="ClipName"/>.
/// </summary>
public readonly record struct AnimationEvent(Entity Entity, string ClipName, string Key);
