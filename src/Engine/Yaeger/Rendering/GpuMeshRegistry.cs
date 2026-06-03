using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Manages GPU-side mesh lifetimes and provides integer-keyed lookups for use with
/// the <see cref="Yaeger.Graphics.MeshHandle"/> ECS component.
/// </summary>
public sealed class GpuMeshRegistry(GL gl) : IDisposable
{
    private readonly Dictionary<int, GpuMesh> _meshes = new();
    private int _nextId;

    /// <summary>Uploads <paramref name="data"/> to the GPU and returns a typed handle.</summary>
    public MeshHandle Register(MeshData data)
    {
        var handle = new MeshHandle(_nextId++);
        _meshes[handle.Id] = new GpuMesh(gl, data);
        return handle;
    }

    /// <summary>Looks up the mesh for the given handle.</summary>
    public bool TryGet(MeshHandle handle, out GpuMesh mesh) =>
        _meshes.TryGetValue(handle.Id, out mesh!);

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();
    }
}
