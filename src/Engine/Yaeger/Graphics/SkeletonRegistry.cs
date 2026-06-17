using System.Diagnostics.CodeAnalysis;

namespace Yaeger.Graphics;

/// <summary>
/// Stores <see cref="Skeleton"/> instances and their animation clips, providing integer-keyed
/// lookups for the <see cref="SkeletonHandle"/> ECS component. Mirrors the role
/// <see cref="Yaeger.Rendering.GpuMeshRegistry"/> plays for meshes, but holds only CPU-side data so
/// it stays platform-agnostic (no GL dependency).
/// </summary>
public sealed class SkeletonRegistry
{
    private sealed record Entry(Skeleton Skeleton, Dictionary<string, AnimationClip> Clips);

    private readonly Dictionary<int, Entry> _entries = new();
    private int _nextId = 1; // 0 is reserved so default(SkeletonHandle) is always invalid

    /// <summary>Registers a skeleton and its clips, returning a typed handle.</summary>
    public SkeletonHandle Register(Skeleton skeleton, IEnumerable<AnimationClip>? clips = null)
    {
        ArgumentNullException.ThrowIfNull(skeleton);

        var clipMap = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        if (clips != null)
        {
            foreach (var clip in clips)
                clipMap[clip.Name] = clip;
        }

        var handle = new SkeletonHandle(_nextId++);
        _entries[handle.Id] = new Entry(skeleton, clipMap);
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
}
