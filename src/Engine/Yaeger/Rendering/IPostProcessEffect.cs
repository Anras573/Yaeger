namespace Yaeger.Rendering;

/// <summary>
/// A single full-screen post-processing pass in a <see cref="PostProcessStack"/>'s effect chain.
/// Implementations own their own <see cref="Shader"/>s (and, for a multi-pass effect, any extra
/// offscreen <see cref="RenderTarget"/>s it needs internally — e.g. bloom's threshold/blur
/// buffers) and are responsible for disposing them.
/// </summary>
public interface IPostProcessEffect : IDisposable
{
    /// <summary>Whether this effect participates in the chain. Toggle at runtime to enable/disable it.</summary>
    bool Enabled { get; set; }

    /// <summary>
    /// True for an effect that must be the last enabled pass in the chain — e.g. <see cref="ToneMapEffect"/>,
    /// which compresses HDR colour down to [0, 1] and gamma-encodes it; an effect placed after it
    /// would operate on already tone-mapped/gamma-encoded values instead of the linear scene colour
    /// it expects. Checked by <see cref="PostProcessPlanner.ValidateOrdering"/>. Default false.
    /// </summary>
    bool RequiresLastPass => false;

    /// <summary>
    /// Runs this effect: read <paramref name="sourceColorTexture"/> (the previous pass's output,
    /// or the rendered scene for the first enabled effect) and end by drawing into
    /// <paramref name="destinationFramebuffer"/> (0 for the window's backbuffer) at
    /// <paramref name="width"/> x <paramref name="height"/>. A single-pass effect binds the
    /// destination immediately; a multi-pass effect (like bloom) may bind its own internal
    /// targets first and must bind <paramref name="destinationFramebuffer"/> again before its
    /// final draw, since <see cref="PostProcessStack"/> does not rebind it afterwards. Use
    /// <paramref name="quad"/> to submit the full-screen triangle pair for every draw.
    /// </summary>
    void Apply(
        uint sourceColorTexture,
        uint destinationFramebuffer,
        int width,
        int height,
        FullscreenQuad quad
    );

    /// <summary>
    /// Resizes any additional offscreen targets this effect owns (e.g. bloom's threshold/blur
    /// buffers) to match the stack's new size. Default no-op for effects that only sample
    /// <c>sourceColorTexture</c> at whatever resolution the stack already gives them.
    /// </summary>
    void Resize(int width, int height) { }
}
