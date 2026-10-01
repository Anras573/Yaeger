using System.Globalization;
using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IGlyphMetricsProvider"/> backed by Canvas 2D glyph atlases in <c>yaeger-browser.js</c>.
/// Glyphs are rasterized once per (family, size, devicePixelRatio) and their metrics cached here,
/// so steady-state text costs no JS calls and no new atlas pages; only unseen characters
/// (one batched call per <see cref="Prepare"/>) hit the browser.
/// </summary>
public sealed class BrowserGlyphAtlas(Func<float> pixelRatio) : IGlyphMetricsProvider
{
    private const int FloatsPerGlyph = 11;

    private readonly Dictionary<AtlasKey, AtlasCache> _atlases = [];
    private readonly List<int> _missing = [];

    public FontLineMetrics GetLineMetrics(string fontKey, int fontSize)
    {
        var cache = GetCache(fontKey, fontSize);
        if (cache.Line is { } line)
            return line;

        var m = JsInterop.FontLineMetrics(fontKey, fontSize, cache.Key.Dpr);
        cache.Line = new FontLineMetrics((float)m[0], (float)m[1]);
        return cache.Line.Value;
    }

    public bool TryGetGlyph(string fontKey, int fontSize, int codepoint, out GlyphMetrics glyph)
    {
        var cache = GetCache(fontKey, fontSize);
        if (!cache.Glyphs.TryGetValue(codepoint, out glyph))
        {
            Ensure(cache, [codepoint]);
            return cache.Glyphs.TryGetValue(codepoint, out glyph);
        }
        return true;
    }

    /// <summary>Rasterizes every not-yet-cached character of <paramref name="text"/> in one JS call.</summary>
    public void Prepare(string fontKey, int fontSize, string text)
    {
        var cache = GetCache(fontKey, fontSize);
        _missing.Clear();
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value >= ' ' && !cache.Glyphs.ContainsKey(rune.Value))
                _missing.Add(rune.Value);
        if (_missing.Count > 0)
            Ensure(cache, _missing.Distinct().ToArray());
    }

    private static void Ensure(AtlasCache cache, int[] codepoints)
    {
        var key = cache.Key;
        var data = JsInterop.GlyphAtlasEnsure(
            key.Family,
            key.Size,
            key.Dpr,
            cache.PathPrefix,
            codepoints
        );
        for (var i = 0; i + FloatsPerGlyph <= data.Length; i += FloatsPerGlyph)
        {
            var page = (int)data[i + 1];
            cache.Glyphs[(int)data[i]] = new GlyphMetrics(
                Advance: (float)data[i + 2],
                OffsetX: (float)data[i + 3],
                OffsetY: (float)data[i + 4],
                Width: (float)data[i + 5],
                Height: (float)data[i + 6],
                UvMin: new Vector2((float)data[i + 7], (float)data[i + 8]),
                UvMax: new Vector2((float)data[i + 9], (float)data[i + 10]),
                TexturePath: page < 0
                    ? ""
                    : cache.PathPrefix + page.ToString(CultureInfo.InvariantCulture)
            );
        }
    }

    private AtlasCache GetCache(string family, int size)
    {
        var dpr = MathF.Round(MathF.Max(pixelRatio(), 1f) * 4f) / 4f;
        var key = new AtlasKey(family, size, dpr);
        if (!_atlases.TryGetValue(key, out var cache))
        {
            cache = new AtlasCache(
                key,
                string.Create(CultureInfo.InvariantCulture, $"font://{family}/{size}@{dpr}/")
            );
            _atlases[key] = cache;
        }
        return cache;
    }

    private readonly record struct AtlasKey(string Family, int Size, float Dpr);

    private sealed class AtlasCache(AtlasKey key, string pathPrefix)
    {
        public AtlasKey Key { get; } = key;
        public string PathPrefix { get; } = pathPrefix;
        public Dictionary<int, GlyphMetrics> Glyphs { get; } = [];
        public FontLineMetrics? Line { get; set; }
    }
}
