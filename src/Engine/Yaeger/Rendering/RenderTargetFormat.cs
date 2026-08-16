namespace Yaeger.Rendering;

/// <summary>
/// Colour attachment format for a <see cref="RenderTarget"/>. Selects between the engine's
/// original 8-bit-per-channel LDR format and a floating-point HDR format that can hold colour
/// values above 1.0 without clamping — see docs/post-processing.md.
/// </summary>
public enum RenderTargetFormat
{
    /// <summary>8-bit unsigned normalized RGBA, clamped to [0, 1]. The engine's original format.</summary>
    Rgba8,

    /// <summary>16-bit float RGBA. Values above 1.0 survive until a tone-mapping pass compresses them.</summary>
    Rgba16F,
}
