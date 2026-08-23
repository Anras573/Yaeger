using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side chunk planning — no GL context needed, unlike the rest of Rendering/.
public class InstancedSkinningPlannerTests
{
    [Fact]
    public void PlanChunks_Empty_ReturnsNoChunks()
    {
        Assert.Empty(InstancedSkinningPlanner.PlanChunks([]));
    }

    [Fact]
    public void PlanChunks_WellUnderCap_ReturnsOneChunkCoveringEveryInstance()
    {
        var boneCounts = Enumerable.Repeat(64, 40).ToList();

        var chunks = InstancedSkinningPlanner.PlanChunks(boneCounts);

        Assert.Equal([(0, 40)], chunks);
    }

    [Fact]
    public void PlanChunks_ExceedingCap_SplitsIntoMultipleChunksCoveringEveryInstance()
    {
        // 5000 four-bone instances = 5000 * 4 * 4 = 80000 texels, comfortably over the 65536 cap,
        // so this must split into more than one chunk.
        var boneCounts = Enumerable.Repeat(4, 5000).ToList();

        var chunks = InstancedSkinningPlanner.PlanChunks(boneCounts);

        Assert.True(chunks.Count > 1);

        // Every instance is covered exactly once, in order, with no gaps — never silently dropped.
        var nextExpectedStart = 0;
        var totalCovered = 0;
        foreach (var (start, count) in chunks)
        {
            Assert.Equal(nextExpectedStart, start);
            Assert.True(count > 0);
            nextExpectedStart = start + count;
            totalCovered += count;
        }
        Assert.Equal(boneCounts.Count, totalCovered);

        // Each chunk itself must respect the cap.
        foreach (var (start, count) in chunks)
        {
            var texels = boneCounts
                .Skip(start)
                .Take(count)
                .Sum(b => b * BonePaletteBuffer.TexelsPerBone);
            Assert.True(texels <= InstancedSkinningPlanner.MaxPaletteTexels);
        }
    }

    [Fact]
    public void PlanChunks_SingleInstanceExceedingCapAlone_StillGetsItsOwnChunkRatherThanBeingDropped()
    {
        // One instance whose own palette alone (20000 bones -> 80000 texels) exceeds the cap must
        // still appear in the plan — never silently dropped.
        var boneCounts = new List<int> { 20000 };

        var chunks = InstancedSkinningPlanner.PlanChunks(boneCounts);

        Assert.Equal([(0, 1)], chunks);
    }

    [Fact]
    public void PlanChunks_InstanceExceedingCapAmongOthers_GetsItsOwnChunk()
    {
        var boneCounts = new List<int> { 4, 4, 20000, 4, 4 };

        var chunks = InstancedSkinningPlanner.PlanChunks(boneCounts);

        // The oversized instance (index 2) can't share a chunk with anything else (it alone exceeds
        // the cap), so it must start its own chunk and end the one before it.
        Assert.Contains(chunks, c => c.Start <= 2 && c.Start + c.Count > 2 && c.Count == 1);

        var nextExpectedStart = 0;
        var totalCovered = 0;
        foreach (var (start, count) in chunks)
        {
            Assert.Equal(nextExpectedStart, start);
            nextExpectedStart = start + count;
            totalCovered += count;
        }
        Assert.Equal(boneCounts.Count, totalCovered);
    }
}
