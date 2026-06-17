using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A single node in a <see cref="Skeleton"/>'s bone hierarchy. <see cref="ParentIndex"/> indexes
/// into the owning skeleton's <c>Bones</c> array, or is <c>-1</c> for the root.
/// <see cref="LocalTransform"/> is the bone's transform relative to its parent in the rest
/// (bind) pose; animations replace it per frame.
/// </summary>
public record Bone(string Name, int ParentIndex, Matrix4x4 LocalTransform);

/// <summary>
/// A bone hierarchy plus the inverse bind-pose matrices used for GPU skinning. Bones are stored in
/// depth-first pre-order, so every bone's <see cref="Bone.ParentIndex"/> is strictly less than its
/// own index — this lets <see cref="ComputeMatrixPalette"/> resolve world transforms in a single
/// forward pass. <see cref="InverseBindPoses"/> is parallel to <see cref="Bones"/>.
/// </summary>
public record Skeleton(Bone[] Bones, Matrix4x4[] InverseBindPoses)
{
    /// <summary>Number of bones in the skeleton.</summary>
    public int BoneCount => Bones.Length;

    /// <summary>
    /// Computes the skinning matrix palette for a pose. <paramref name="localTransforms"/> holds the
    /// per-bone local (parent-relative) transforms for the pose; entries beyond its length fall back
    /// to the bone's bind-pose <see cref="Bone.LocalTransform"/>. The result written to
    /// <paramref name="palette"/> is, per bone, <c>InverseBindPose * worldTransform</c> — the matrix
    /// the vertex shader multiplies skinned vertices by.
    /// </summary>
    public void ComputeMatrixPalette(
        ReadOnlySpan<Matrix4x4> localTransforms,
        Span<Matrix4x4> palette
    )
    {
        if (palette.Length < Bones.Length)
            throw new ArgumentException(
                $"Palette span must hold at least {Bones.Length} matrices.",
                nameof(palette)
            );

        // Pass 1: accumulate world transforms. Bones are pre-order, so a parent's world transform is
        // already resolved by the time we reach its children. world = local * parentWorld in the
        // row-vector convention System.Numerics uses.
        for (var i = 0; i < Bones.Length; i++)
        {
            var local = i < localTransforms.Length ? localTransforms[i] : Bones[i].LocalTransform;
            var parent = Bones[i].ParentIndex;
            palette[i] = parent >= 0 && parent < i ? local * palette[parent] : local;
        }

        // Pass 2: fold in the inverse bind pose. A vertex in bind/model space is first pulled into
        // bone space (InverseBindPose) and then pushed out by the animated world transform.
        for (var i = 0; i < Bones.Length; i++)
            palette[i] = InverseBindPoses[i] * palette[i];
    }
}
