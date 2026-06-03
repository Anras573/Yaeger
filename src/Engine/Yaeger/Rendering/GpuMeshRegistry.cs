using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

/// <summary>
/// Manages GPU-side mesh lifetimes and provides integer-keyed lookups for use with
/// the <see cref="Yaeger.Graphics.MeshHandle"/> ECS component.
/// </summary>
public sealed class GpuMeshRegistry(GL gl) : IDisposable
{
    private readonly Dictionary<int, GpuMesh> _meshes = new();
    private int _nextId;

    /// <summary>Uploads <paramref name="data"/> to the GPU and returns an opaque handle ID.</summary>
    public int Register(MeshData data)
    {
        var id = _nextId++;
        _meshes[id] = new GpuMesh(gl, data);
        return id;
    }

    /// <summary>Looks up the mesh for the given handle ID.</summary>
    public bool TryGet(int id, out GpuMesh mesh) => _meshes.TryGetValue(id, out mesh!);

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();
    }
}
