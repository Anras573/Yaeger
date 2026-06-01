namespace Yaeger.Rendering;

public enum TextRenderMode
{
    /// <summary>
    /// Alpha-coverage rendering. Glyphs are rasterised at the requested pixel size and
    /// composited with a simple coverage shader. Cheap but blurs when scaled.
    /// </summary>
    Standard,

    /// <summary>
    /// Signed-distance-field rendering. Each glyph is rasterised at 2× the atlas size,
    /// a CPU distance-transform is applied, and the result is downsampled to the atlas.
    /// The SDF fragment shader uses <c>smoothstep</c> on the stored distance to reconstruct
    /// a crisp edge at any render scale.
    /// </summary>
    Sdf,
}
