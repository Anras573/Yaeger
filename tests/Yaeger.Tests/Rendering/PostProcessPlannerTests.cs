using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side pass-ordering logic — no GL context needed, unlike the rest of Rendering/.
public class PostProcessPlannerTests
{
    [Fact]
    public void Plan_NoEnabledEffects_ReturnsEmpty()
    {
        var passes = PostProcessPlanner.Plan([]);

        Assert.Empty(passes);
    }

    [Fact]
    public void Plan_SingleEffect_ReadsSceneAndWritesBackbuffer()
    {
        var passes = PostProcessPlanner.Plan([0]);

        var pass = Assert.Single(passes);
        Assert.Equal(0, pass.EffectIndex);
        Assert.Equal(PostProcessSurface.Scene, pass.Source);
        Assert.Equal(PostProcessSurface.Backbuffer, pass.Destination);
    }

    [Fact]
    public void Plan_TwoEffects_PingPongsThroughPingPongA()
    {
        var passes = PostProcessPlanner.Plan([0, 1]);

        Assert.Equal(2, passes.Count);
        Assert.Equal(
            new PostProcessPlanner.Pass(0, PostProcessSurface.Scene, PostProcessSurface.PingPongA),
            passes[0]
        );
        Assert.Equal(
            new PostProcessPlanner.Pass(
                1,
                PostProcessSurface.PingPongA,
                PostProcessSurface.Backbuffer
            ),
            passes[1]
        );
    }

    [Fact]
    public void Plan_ThreeEffects_AlternatesBothPingPongTargets()
    {
        var passes = PostProcessPlanner.Plan([0, 1, 2]);

        Assert.Equal(3, passes.Count);
        Assert.Equal(
            new PostProcessPlanner.Pass(0, PostProcessSurface.Scene, PostProcessSurface.PingPongA),
            passes[0]
        );
        Assert.Equal(
            new PostProcessPlanner.Pass(
                1,
                PostProcessSurface.PingPongA,
                PostProcessSurface.PingPongB
            ),
            passes[1]
        );
        Assert.Equal(
            new PostProcessPlanner.Pass(
                2,
                PostProcessSurface.PingPongB,
                PostProcessSurface.Backbuffer
            ),
            passes[2]
        );
    }

    [Fact]
    public void Plan_PreservesEffectIndicesForSkippedDisabledEffects()
    {
        // Effects at indices 1 and 3 are disabled and never appear; the surviving indices (2, 5)
        // are not contiguous, so the plan must carry the *original* indices through, not 0/1.
        var passes = PostProcessPlanner.Plan([2, 5]);

        Assert.Equal(2, passes.Count);
        Assert.Equal(2, passes[0].EffectIndex);
        Assert.Equal(5, passes[1].EffectIndex);
    }

    [Fact]
    public void Plan_EachPassReadsThePreviousPassDestination()
    {
        var passes = PostProcessPlanner.Plan([0, 1, 2, 3, 4]);

        for (var i = 1; i < passes.Count; i++)
            Assert.Equal(passes[i - 1].Destination, passes[i].Source);
    }

    [Fact]
    public void Plan_NoPassEverReadsWhatItItselfWrites()
    {
        var passes = PostProcessPlanner.Plan([0, 1, 2, 3, 4]);

        Assert.All(passes, pass => Assert.NotEqual(pass.Source, pass.Destination));
    }

    [Fact]
    public void Plan_OnlyTheLastPassWritesBackbuffer()
    {
        var passes = PostProcessPlanner.Plan([0, 1, 2, 3]);

        for (var i = 0; i < passes.Count - 1; i++)
            Assert.NotEqual(PostProcessSurface.Backbuffer, passes[i].Destination);

        Assert.Equal(PostProcessSurface.Backbuffer, passes[^1].Destination);
    }

    [Fact]
    public void SelectSceneFormat_HdrEnabled_ReturnsRgba16F()
    {
        Assert.Equal(RenderTargetFormat.Rgba16F, PostProcessPlanner.SelectSceneFormat(hdr: true));
    }

    [Fact]
    public void SelectSceneFormat_HdrDisabled_ReturnsRgba8()
    {
        Assert.Equal(RenderTargetFormat.Rgba8, PostProcessPlanner.SelectSceneFormat(hdr: false));
    }

    [Fact]
    public void ValidateOrdering_NoEffectRequiresLast_DoesNotThrow()
    {
        PostProcessPlanner.ValidateOrdering([0, 1, 2], _ => false);
    }

    [Fact]
    public void ValidateOrdering_RequiresLastEffectIsLast_DoesNotThrow()
    {
        PostProcessPlanner.ValidateOrdering([0, 1, 2], index => index == 2);
    }

    [Fact]
    public void ValidateOrdering_RequiresLastEffectFollowedByAnother_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PostProcessPlanner.ValidateOrdering([0, 1, 2], index => index == 1)
        );
    }

    [Fact]
    public void ValidateOrdering_SingleRequiresLastEffect_DoesNotThrow()
    {
        // Only one effect in the chain: nothing follows it regardless of RequiresLastPass.
        PostProcessPlanner.ValidateOrdering([0], _ => true);
    }

    [Fact]
    public void ValidateOrdering_Empty_DoesNotThrow()
    {
        PostProcessPlanner.ValidateOrdering([], _ => true);
    }
}
