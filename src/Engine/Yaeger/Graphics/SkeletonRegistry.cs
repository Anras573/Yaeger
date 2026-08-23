using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Stores <see cref="Skeleton"/> instances and their animation clips, providing integer-keyed
/// lookups for the <see cref="SkeletonHandle"/> ECS component. Mirrors the role
/// <see cref="Yaeger.Rendering.GpuMeshRegistry"/> plays for meshes, but holds only CPU-side data so
/// it stays platform-agnostic (no GL dependency).
/// </summary>
public sealed class SkeletonRegistry
{
    private sealed record Entry(
        Skeleton Skeleton,
        Dictionary<string, AnimationClip> Clips,
        SkeletonBoneBounds? BoneBounds
    );

    private readonly Dictionary<int, Entry> _entries = new();
    private int _nextId = 1; // 0 is reserved so default(SkeletonHandle) is always invalid

    /// <summary>
    /// Registers a skeleton and its clips, returning a typed handle. When <paramref name="vertices"/>
    /// is supplied, also computes each bone's bind-pose pivot and maximum influence radius (see
    /// <see cref="SkeletonBoneBounds"/>) — enabling <see cref="Yaeger.Systems.SkeletalAnimationSystem"/>
    /// to write a per-frame <see cref="Aabb3D"/> for frustum culling. Omitting it (the default) leaves
    /// entities driven by this skeleton uncullable, matching the pre-existing behaviour.
    /// </summary>
    public SkeletonHandle Register(
        Skeleton skeleton,
        IEnumerable<AnimationClip>? clips = null,
        IEnumerable<SkinnedVertex>? vertices = null
    )
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var clipMap = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        if (clips != null)
        {
            foreach (var clip in clips)
                clipMap[clip.Name] = clip;
        }

        var boneBounds = vertices != null ? ComputeBoneBounds(skeleton, vertices) : null;

        var handle = new SkeletonHandle(_nextId++);
        _entries[handle.Id] = new Entry(skeleton, clipMap, boneBounds);
        return handle;
    }

    /// <summary>Looks up the skeleton for the given handle.</summary>
    public bool TryGet(SkeletonHandle handle, [NotNullWhen(true)] out Skeleton? skeleton)
    {
        if (_entries.TryGetValue(handle.Id, out var entry))
        {
            skeleton = entry.Skeleton;
            return true;
        }
        skeleton = null;
        return false;
    }

    /// <summary>Looks up a named clip for the given handle.</summary>
    public bool TryGetClip(
        SkeletonHandle handle,
        string clipName,
        [NotNullWhen(true)] out AnimationClip? clip
    )
    {
        clip = null;
        return _entries.TryGetValue(handle.Id, out var entry)
            && entry.Clips.TryGetValue(clipName, out clip);
    }

    /// <summary>Returns the names of all clips registered for the given handle.</summary>
    public IReadOnlyCollection<string> GetClipNames(SkeletonHandle handle) =>
        _entries.TryGetValue(handle.Id, out var entry)
            ? entry.Clips.Keys
            : (IReadOnlyCollection<string>)Array.Empty<string>();

    /// <summary>
    /// Looks up the per-bone bounds computed at <see cref="Register"/> time. Returns <c>false</c>
    /// when the handle is unknown or <see cref="Register"/> was called without vertex data.
    /// </summary>
    public bool TryGetBoneBounds(
        SkeletonHandle handle,
        [NotNullWhen(true)] out SkeletonBoneBounds? boneBounds
    )
    {
        if (_entries.TryGetValue(handle.Id, out var entry) && entry.BoneBounds != null)
        {
            boneBounds = entry.BoneBounds;
            return true;
        }
        boneBounds = null;
        return false;
    }

    // Computes, per bone, the bind-pose world position (the pivot the resolved skinning palette
    // carries every frame) and the farthest any vertex with a non-zero weight to that bone sits from
    // that pivot in bind space. The bind-pose pivot is recovered from InverseBindPoses[i] alone
    // (its translation once inverted) rather than composing the bone hierarchy again, since the two
    // are exact inverses of each other by construction.
    private static SkeletonBoneBounds ComputeBoneBounds(
        Skeleton skeleton,
        IEnumerable<SkinnedVertex> vertices
    )
    {
        var boneCount = skeleton.BoneCount;
        var bindPositions = new Vector3[boneCount];
        for (var i = 0; i < boneCount; i++)
        {
            bindPositions[i] = Matrix4x4.Invert(skeleton.InverseBindPoses[i], out var bindWorld)
                ? bindWorld.Translation
                : Vector3.Zero;
        }

        var radii = new float[boneCount];
        foreach (var vertex in vertices)
        {
            AccumulateRadius(
                vertex.Position,
                vertex.BoneIndices.X,
                vertex.BoneWeights.X,
                bindPositions,
                radii
            );
            AccumulateRadius(
                vertex.Position,
                vertex.BoneIndices.Y,
                vertex.BoneWeights.Y,
                bindPositions,
                radii
            );
            AccumulateRadius(
                vertex.Position,
                vertex.BoneIndices.Z,
                vertex.BoneWeights.Z,
                bindPositions,
                radii
            );
            AccumulateRadius(
                vertex.Position,
                vertex.BoneIndices.W,
                vertex.BoneWeights.W,
                bindPositions,
                radii
            );
        }

        return new SkeletonBoneBounds(bindPositions, radii);
    }

    private static void AccumulateRadius(
        Vector3 position,
        float boneIndexF,
        float weight,
        Vector3[] bindPositions,
        float[] radii
    )
    {
        if (weight <= 0f)
            return;

        var boneIndex = (int)MathF.Round(boneIndexF);
        if (boneIndex < 0 || boneIndex >= bindPositions.Length)
            return;

        var distance = Vector3.Distance(position, bindPositions[boneIndex]);
        if (distance > radii[boneIndex])
            radii[boneIndex] = distance;
    }
}
