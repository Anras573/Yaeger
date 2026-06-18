using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// ECS component holding the per-frame skinning matrix palette for a skinned mesh entity. Written by
/// the <see cref="Yaeger.Systems.SkeletalAnimationSystem"/> and consumed by the mesh render system,
/// which uploads it to the GPU. The array is reused across frames to avoid per-frame allocation.
/// </summary>
public struct BonePalette
{
    /// <summary>Skinning matrices, one per skeleton bone (<c>InverseBindPose * worldTransform</c>).</summary>
    public Matrix4x4[] Matrices;

    public BonePalette(Matrix4x4[] matrices) => Matrices = matrices;
}
