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

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
        {
            texture.Dispose();
        }
        _cache.Clear();
    }
}
