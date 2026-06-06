using System.Diagnostics.CodeAnalysis;
using Silk.NET.OpenGL;
using Yaeger.Graphics;

namespace Yaeger.Rendering;

/// <summary>
/// Manages cubemap texture lifetimes and provides integer-keyed lookups for use with
/// the <see cref="Skybox"/> ECS component.
/// </summary>
public sealed class CubemapRegistry(GL gl) : IDisposable
{
    private readonly Dictionary<int, CubemapTexture> _cubemaps = new();
    private int _nextId = 1; // 0 is reserved so default(Skybox) is always invalid

    /// <summary>
    /// Loads six face images into a new cubemap texture and returns a typed handle.
    /// Face order: right (+X), left (−X), top (+Y), bottom (−Y), front (+Z), back (−Z).
    /// </summary>
    public Skybox Register(
        string right,
        string left,
        string top,
        string bottom,
        string front,
        string back
    )
    {
        var handle = new Skybox(_nextId++);
        _cubemaps[handle.Id] = new CubemapTexture(gl, right, left, top, bottom, front, back);
        return handle;
    }

    /// <summary>Looks up the cubemap texture for the given handle.</summary>
    public bool TryGet(Skybox handle, [NotNullWhen(true)] out CubemapTexture? cubemap) =>
        _cubemaps.TryGetValue(handle.Id, out cubemap);

    public void Dispose()
    {
        foreach (var cubemap in _cubemaps.Values)
            cubemap.Dispose();
        _cubemaps.Clear();
    }
}
