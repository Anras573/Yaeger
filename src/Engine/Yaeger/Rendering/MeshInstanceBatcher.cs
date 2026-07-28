using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Groups per-frame mesh draw submissions by (<see cref="MeshHandle"/>, <see cref="Material3D"/>) so
/// <see cref="Systems.MeshRenderSystem"/> can route a large group through
/// <see cref="Renderer3D.DrawInstanced"/> (one <c>glDrawElementsInstanced</c> call) instead of issuing
/// one draw call per entity. Pure CPU-side bookkeeping — no GL calls, so this is unit-testable without
/// a live OpenGL context.
///
/// Groups (and their backing lists) persist across <see cref="Clear"/> calls so a scene whose mesh/
/// material composition is stable frame-to-frame doesn't reallocate once warmed up — the same pattern
/// <c>MeshRenderSystem</c> already uses for its point/spot light scratch buffers.
/// </summary>
public sealed class MeshInstanceBatcher
{
    /// <summary>A group of model matrices sharing the same mesh and material.</summary>
    public readonly record struct Group(
        MeshHandle Handle,
        Material3D Material,
        List<Matrix4x4> Models
    );

    private readonly Dictionary<(MeshHandle Handle, Material3D Material), List<Matrix4x4>> _groups =
        new();

    /// <summary>Adds a model matrix to the group sharing its mesh handle and material.</summary>
    public void Add(MeshHandle handle, Material3D material, Matrix4x4 model)
    {
        var key = (handle, material);
        if (!_groups.TryGetValue(key, out var models))
        {
            models = new List<Matrix4x4>();
            _groups[key] = models;
        }
        models.Add(model);
    }

    /// <summary>The non-empty groups accumulated since the last <see cref="Clear"/>.</summary>
    public IEnumerable<Group> Groups
    {
        get
        {
            foreach (var (key, models) in _groups)
            {
                if (models.Count > 0)
                    yield return new Group(key.Handle, key.Material, models);
            }
        }
    }

    /// <summary>Empties every group's list (keeping its capacity) so the batcher can be reused next frame.</summary>
    public void Clear()
    {
        foreach (var models in _groups.Values)
            models.Clear();
    }
}
