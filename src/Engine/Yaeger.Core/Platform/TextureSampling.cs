namespace Yaeger.Platform;

/// <summary>How a texture is filtered when sampled at a size other than its native resolution.</summary>
public enum TextureFilter
{
    /// <summary>Point sampling — crisp texels, the right choice for pixel art.</summary>
    Nearest,

    /// <summary>Bilinear filtering with no mipmaps. Safe for atlases (mips would average neighbouring entries).</summary>
    Linear,

    /// <summary>Trilinear filtering through a generated mip chain. Best for large minified 3D textures.</summary>
    LinearMipmap,
}

/// <summary>How texture coordinates outside [0, 1] are handled.</summary>
public enum TextureWrap
{
    /// <summary>Clamp to the edge texel.</summary>
    Clamp,

    /// <summary>Tile the texture.</summary>
    Repeat,
}

/// <summary>
/// Sampling state for a texture, shared by the native and browser runtimes so the same texture
/// is sampled identically on both.
/// </summary>
public readonly record struct TextureSampling(TextureFilter Filter, TextureWrap Wrap)
{
    /// <summary>Engine-wide default: <see cref="TextureFilter.Linear"/> + <see cref="TextureWrap.Clamp"/>.</summary>
    public static TextureSampling Default => new(TextureFilter.Linear, TextureWrap.Clamp);

    /// <summary>Crisp pixel-art sampling: <see cref="TextureFilter.Nearest"/> + <see cref="TextureWrap.Clamp"/>.</summary>
    public static TextureSampling Pixelated => new(TextureFilter.Nearest, TextureWrap.Clamp);

    /// <summary>Tiling, mip-mapped sampling suited to 3D material textures.</summary>
    public static TextureSampling Tiled => new(TextureFilter.LinearMipmap, TextureWrap.Repeat);

    /// <summary>Whether a mip chain must be generated for this sampling.</summary>
    public bool UsesMipmaps => Filter == TextureFilter.LinearMipmap;
}

/// <summary>
/// A default <see cref="TextureSampling"/> plus per-texture-path overrides. Pure bookkeeping
/// shared by the native <c>TextureManager</c> and the browser render surface.
/// </summary>
public sealed class TextureSamplingTable(TextureSampling defaultSampling)
{
    private readonly Dictionary<string, TextureSampling> _overrides = new();

    /// <summary>Sampling used by any path without an override.</summary>
    public TextureSampling Default { get; set; } = defaultSampling;

    public TextureSamplingTable()
        : this(TextureSampling.Default) { }

    /// <summary>Sets the sampling override for <paramref name="path"/>.</summary>
    public void Set(string path, TextureSampling sampling) => _overrides[path] = sampling;

    /// <summary>Removes the override for <paramref name="path"/>, reverting it to <see cref="Default"/>.</summary>
    public bool Clear(string path) => _overrides.Remove(path);

    /// <summary>The effective sampling for <paramref name="path"/>.</summary>
    public TextureSampling Get(string path) =>
        _overrides.TryGetValue(path, out var s) ? s : Default;
}
