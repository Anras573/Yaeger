using Silk.NET.OpenGL;
using Yaeger.Platform;

namespace Yaeger.Rendering;

public class TextureManager(GL gl, TextureSampling? defaultSampling = null) : IDisposable
{
    private readonly TextureSamplingTable _sampling = new(
        defaultSampling ?? TextureSampling.Default
    );

    /// <summary>Sampling for textures with no per-path override. Applies to textures loaded afterwards.</summary>
    public TextureSampling DefaultSampling
    {
        get => _sampling.Default;
        set => _sampling.Default = value;
    }

    /// <summary>
    /// Overrides filtering/wrapping for <paramref name="path"/>; applied immediately if the
    /// texture is already loaded, otherwise on load.
    /// </summary>
    public void SetSampling(string path, TextureSampling sampling)
    {
        _sampling.Set(path, sampling);
        if (_cache.TryGetValue(path, out var texture))
            texture.SetSampling(sampling);
    }

    private readonly Dictionary<string, Texture> _cache = new();

    public Texture Get(string path)
    {
        if (_cache.TryGetValue(path, out var texture))
            return texture;
        // The empty path is the engine-wide "no texture" sentinel (IRenderSurface.SolidTexturePath):
        // it resolves to a 1x1 white texture so tinted quads render as flat colour.
        texture = path.Length == 0 ? new Texture(gl) : new Texture(gl, path, _sampling.Get(path));
        _cache[path] = texture;

        return texture;
    }

    /// <summary>
    /// Re-uploads the file at <paramref name="path"/> into the existing cached <see cref="Texture"/>
    /// for that path, if one exists — every <c>Sprite</c>/<c>Material3D</c> referencing the path
    /// picks up the change with no handle churn. A no-op (returns <see langword="false"/>) if
    /// nothing has ever called <see cref="Get"/> for this path, and safe to call for a path that
    /// isn't a texture at all (e.g. wiring a single watcher callback for every changed asset).
    /// </summary>
    public bool Reload(string path) =>
        _cache.TryGetValue(path, out var texture) && texture.TryReload();

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
        {
            texture.Dispose();
        }
        _cache.Clear();
    }
}
