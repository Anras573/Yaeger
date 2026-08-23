using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Groups per-frame mesh draw submissions by (<see cref="MeshHandle"/>, <see cref="Material3D"/>) —
/// or, for skinned entities, by (<see cref="MeshHandle"/>, <see cref="Material3D"/>,
/// <see cref="SkeletonHandle"/>) — so <see cref="Systems.MeshRenderSystem"/> can route a large group
/// through <see cref="Renderer3D.DrawInstanced"/>/<see cref="Renderer3D.DrawInstancedSkinned"/> (one
/// or a handful of <c>glDrawElementsInstanced</c> calls) instead of issuing one draw call per entity.
/// Pure CPU-side bookkeeping — no GL calls, so this is unit-testable without a live OpenGL context.
///
/// Groups (and their backing lists) persist across <see cref="Clear"/> calls so a scene whose mesh/
/// material/skeleton composition is stable frame-to-frame doesn't reallocate once warmed up — the
/// same pattern <c>MeshRenderSystem</c> already uses for its point/spot light scratch buffers.
/// </summary>
public sealed class MeshInstanceBatcher
{
    /// <summary>
    /// A group of instances sharing the same mesh, material, and (for skinned instances) skeleton.
    /// <paramref name="Skeleton"/> is <c>default</c> for a group added via <see cref="Add"/> (static,
    /// non-skinned instances); <see cref="IsSkinned"/> distinguishes the two. For a skinned group,
    /// <paramref name="BonePalettes"/> and <paramref name="PaletteOffsets"/> are parallel to
    /// <paramref name="Models"/>: entry <c>i</c> is that instance's own resolved skinning matrices and
    /// its cumulative starting bone-slot offset into the group's palettes packed back-to-back (i.e.
    /// the sum of the bone counts of every earlier instance in the group) — see
    /// <see cref="Renderer3D.DrawInstancedSkinned"/>. Both are empty for a non-skinned group.
    /// </summary>
    public readonly record struct Group(
        MeshHandle Handle,
        Material3D Material,
        SkeletonHandle Skeleton,
        List<Matrix4x4> Models,
        List<Matrix4x4[]> BonePalettes,
        List<int> PaletteOffsets
    )
    {
        /// <summary>True when this group was populated via <see cref="AddSkinned"/>.</summary>
        public bool IsSkinned => Skeleton != default;
    }

    // Holds one group's accumulated instances. BonePalettes/PaletteOffsets stay empty for a group
    // that's only ever populated via Add (Skeleton == default) — no allocation for the common
    // non-skinned case.
    private sealed class GroupData
    {
        public readonly List<Matrix4x4> Models = new();
        public readonly List<Matrix4x4[]> BonePalettes = new();
        public readonly List<int> PaletteOffsets = new();
        public int BoneCursor;
    }

    private readonly Dictionary<
        (MeshHandle Handle, Material3D Material, SkeletonHandle Skeleton),
        GroupData
    > _groups = new();

    /// <summary>Adds a static (non-skinned) instance to the group sharing its mesh handle and material.</summary>
    public void Add(MeshHandle handle, Material3D material, Matrix4x4 model) =>
        GetOrCreate(handle, material, default).Models.Add(model);

    /// <summary>
    /// Adds a skinned instance to the group sharing its mesh handle, material, and skeleton — a
    /// separate axis from the static grouping <see cref="Add"/> uses, since instances sharing a mesh
    /// and material but different skeletons (different bone counts/hierarchies) can't share one
    /// instanced draw's bone-palette buffer. <paramref name="bonePalette"/> is this instance's own
    /// resolved skinning matrices (see <see cref="BonePalette"/>); its length becomes this instance's
    /// contribution to the group's cumulative <see cref="Group.PaletteOffsets"/>.
    /// </summary>
    public void AddSkinned(
        MeshHandle handle,
        Material3D material,
        SkeletonHandle skeleton,
        Matrix4x4 model,
        Matrix4x4[] bonePalette
    )
    {
        ArgumentNullException.ThrowIfNull(bonePalette);

        var data = GetOrCreate(handle, material, skeleton);
        data.Models.Add(model);
        data.BonePalettes.Add(bonePalette);
        data.PaletteOffsets.Add(data.BoneCursor);
        data.BoneCursor += bonePalette.Length;
    }

    private GroupData GetOrCreate(MeshHandle handle, Material3D material, SkeletonHandle skeleton)
    {
        var key = (handle, material, skeleton);
        if (!_groups.TryGetValue(key, out var data))
        {
            data = new GroupData();
            _groups[key] = data;
        }
        return data;
    }

    /// <summary>The non-empty groups accumulated since the last <see cref="Clear"/>.</summary>
    public IEnumerable<Group> Groups
    {
        get
        {
            foreach (var (key, data) in _groups)
            {
                if (data.Models.Count > 0)
                    yield return new Group(
                        key.Handle,
                        key.Material,
                        key.Skeleton,
                        data.Models,
                        data.BonePalettes,
                        data.PaletteOffsets
                    );
            }
        }
    }

    /// <summary>Empties every group's lists (keeping their capacity) so the batcher can be reused next frame.</summary>
    public void Clear()
    {
        foreach (var data in _groups.Values)
        {
            data.Models.Clear();
            data.BonePalettes.Clear();
            data.PaletteOffsets.Clear();
            data.BoneCursor = 0;
        }
    }
}
