using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A single skinned vertex's bind-pose position plus up to four bone influences — the same shape as
/// <c>Yaeger.Rendering.Vertex3D</c>'s skinning fields, kept as a separate type here so
/// <see cref="SkeletonRegistry"/> (platform-agnostic, no rendering dependency) doesn't need to
/// reference the GPU vertex layout. Used only by <see cref="SkeletonRegistry.Register"/> to compute
/// each bone's maximum influence radius at registration time.
/// </summary>
public readonly record struct SkinnedVertex(
    Vector3 Position,
    Vector4 BoneIndices,
    Vector4 BoneWeights
);
