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
}
