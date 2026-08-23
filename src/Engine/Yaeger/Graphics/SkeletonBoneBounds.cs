using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Per-bone bind-pose pivot position and maximum influence radius for a registered
/// <see cref="Skeleton"/>, computed once by <see cref="SkeletonRegistry.Register"/> from the skinned
/// mesh's vertex data. Both arrays are parallel to <see cref="Skeleton.Bones"/>.
/// <see cref="Yaeger.Systems.SkeletalAnimationSystem"/> transforms each <see cref="BindPositions"/>
/// entry by the resolved skinning palette every frame and expands it by the matching
/// <see cref="Radii"/> entry to bound the animated pose, so the per-frame cost stays proportional to
/// bone count rather than vertex count.
/// </summary>
public sealed record SkeletonBoneBounds(Vector3[] BindPositions, float[] Radii);
