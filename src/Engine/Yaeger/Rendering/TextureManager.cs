using Silk.NET.OpenGL;

namespace Yaeger.Rendering;

public class TextureManager(GL gl) : IDisposable
{
    private readonly Dictionary<string, Texture> _cache = new();

    public Texture Get(string path)
    {
        if (_cache.TryGetValue(path, out var texture))
            return texture;
        texture = new Texture(gl, path);
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
