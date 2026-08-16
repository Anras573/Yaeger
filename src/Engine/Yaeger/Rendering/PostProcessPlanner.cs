namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side pass ordering for <see cref="PostProcessStack"/>'s effect chain: given the
/// indices of the currently-enabled effects (in chain order), decides which <see cref="PostProcessSurface"/>
/// each pass reads from and writes to so consecutive passes ping-pong between two offscreen
/// targets instead of every effect needing its own dedicated target. No GL calls — see
/// <see cref="PostProcessStack"/> for the renderer that executes the plan.
/// </summary>
public static class PostProcessPlanner
{
    /// <summary>One post-processing pass: read <see cref="Source"/>, run the effect at <see cref="EffectIndex"/>, write <see cref="Destination"/>.</summary>
    public readonly record struct Pass(
        int EffectIndex,
        PostProcessSurface Source,
        PostProcessSurface Destination
    );

    /// <summary>
    /// Plans passes for <paramref name="enabledEffectIndices"/>, in the order given. The first
    /// pass always reads <see cref="PostProcessSurface.Scene"/>; the last pass always writes
    /// <see cref="PostProcessSurface.Backbuffer"/>; passes in between alternate
    /// <see cref="PostProcessSurface.PingPongA"/>/<see cref="PostProcessSurface.PingPongB"/> so a
    /// pass never reads the surface it (or the pass before it) just wrote. Returns an empty list
    /// for no enabled effects — the caller blits <see cref="PostProcessSurface.Scene"/> straight to
    /// <see cref="PostProcessSurface.Backbuffer"/> in that case, since there is no effect to plan a
    /// pass around.
    /// </summary>
    public static IReadOnlyList<Pass> Plan(IReadOnlyList<int> enabledEffectIndices)
    {
        ArgumentNullException.ThrowIfNull(enabledEffectIndices);

        var passes = new List<Pass>(enabledEffectIndices.Count);
        var source = PostProcessSurface.Scene;
        var nextPingPong = PostProcessSurface.PingPongA;

        for (var i = 0; i < enabledEffectIndices.Count; i++)
        {
            var isLast = i == enabledEffectIndices.Count - 1;
            var destination = isLast ? PostProcessSurface.Backbuffer : nextPingPong;

            passes.Add(new Pass(enabledEffectIndices[i], source, destination));

            source = destination;
            nextPingPong =
                nextPingPong == PostProcessSurface.PingPongA
                    ? PostProcessSurface.PingPongB
                    : PostProcessSurface.PingPongA;
        }

        return passes;
    }

    /// <summary>
    /// Picks the colour format <see cref="PostProcessStack"/>'s scene and ping-pong targets should
    /// use: <see cref="RenderTargetFormat.Rgba16F"/> when HDR is enabled (so values above 1.0
    /// survive for bloom/tone mapping to work with), <see cref="RenderTargetFormat.Rgba8"/>
    /// otherwise — the original, byte-identical LDR format.
    /// </summary>
    public static RenderTargetFormat SelectSceneFormat(bool hdr) =>
        hdr ? RenderTargetFormat.Rgba16F : RenderTargetFormat.Rgba8;

    /// <summary>
    /// Validates that no effect requiring the last position in the chain (<see cref="IPostProcessEffect.RequiresLastPass"/>
    /// — e.g. tone mapping) is followed by another enabled effect. Pure validation, no GL calls;
    /// <paramref name="requiresLastPass"/> maps an effect index (as they appear in
    /// <paramref name="enabledEffectIndices"/>) to its <see cref="IPostProcessEffect.RequiresLastPass"/>
    /// value. Throws <see cref="InvalidOperationException"/> on a violation.
    /// </summary>
    public static void ValidateOrdering(
        IReadOnlyList<int> enabledEffectIndices,
        Func<int, bool> requiresLastPass
    )
    {
        ArgumentNullException.ThrowIfNull(enabledEffectIndices);
        ArgumentNullException.ThrowIfNull(requiresLastPass);

        for (var i = 0; i < enabledEffectIndices.Count - 1; i++)
        {
            var effectIndex = enabledEffectIndices[i];
            if (requiresLastPass(effectIndex))
                throw new InvalidOperationException(
                    $"Post-process effect at index {effectIndex} requires the last position in the "
                        + "chain (e.g. tone mapping) but other enabled effects follow it."
                );
        }
    }
}
