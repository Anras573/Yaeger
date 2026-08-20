using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class LightFlickerSignalTests
{
    // ── Sample ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sample_StaysWithinUnitRange()
    {
        var random = new Random(1);

        for (var i = 0; i < 500; i++)
        {
            var time = (float)(random.NextDouble() * 200);
            var frequency = (float)(random.NextDouble() * 10);
            var seed = (float)(random.NextDouble() * 100 - 50);

            var value = LightFlickerSignal.Sample(time, frequency, seed);

            Assert.InRange(value, -1f, 1f);
        }
    }

    [Fact]
    public void Sample_IsDeterministic()
    {
        var a = LightFlickerSignal.Sample(12.3f, 3f, 7f);
        var b = LightFlickerSignal.Sample(12.3f, 3f, 7f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Sample_IsContinuousOverTime()
    {
        var random = new Random(2);

        for (var i = 0; i < 100; i++)
        {
            var time = (float)(random.NextDouble() * 50);
            var a = LightFlickerSignal.Sample(time, 3f, 1f);
            var b = LightFlickerSignal.Sample(time + 0.0001f, 3f, 1f);

            Assert.True(MathF.Abs(a - b) < 0.01f);
        }
    }

    [Fact]
    public void Sample_IsFramerateIndependent_SameElapsedTimeSamplesTheSameValue()
    {
        // The signal is a pure function of accumulated time, so the same wall-clock elapsed time
        // must sample the same value regardless of how many steps of what size got there.
        const float frequency = 4f;
        const float seed = 2f;

        var manySmallSteps = 0f;
        for (var i = 0; i < 300; i++)
            manySmallSteps += 0.01f;

        var fewLargeSteps = 0f;
        for (var i = 0; i < 3; i++)
            fewLargeSteps += 1f;

        var a = LightFlickerSignal.Sample(manySmallSteps, frequency, seed);
        var b = LightFlickerSignal.Sample(fewLargeSteps, frequency, seed);

        // Tolerates the tiny floating-point drift accumulating 300 small additions leaves versus
        // 3 large ones — the point is that the *signal* is a pure function of elapsed time, not
        // that float summation is bit-exact.
        Assert.True(MathF.Abs(a - b) < 0.01f);
    }

    [Fact]
    public void Sample_DifferentSeeds_ProduceDifferentOutput()
    {
        var random = new Random(3);
        var differed = false;

        for (var i = 0; i < 20; i++)
        {
            var time = (float)(random.NextDouble() * 50);
            var a = LightFlickerSignal.Sample(time, 3f, 1f);
            var b = LightFlickerSignal.Sample(time, 3f, 2f);

            if (MathF.Abs(a - b) > 0.001f)
                differed = true;
        }

        Assert.True(differed);
    }

    [Fact]
    public void Sample_ZeroFrequency_IsConstantOverTime()
    {
        var a = LightFlickerSignal.Sample(0f, 0f, 5f);
        var b = LightFlickerSignal.Sample(100f, 0f, 5f);

        Assert.Equal(a, b);
    }

    // ── SampleOffset ─────────────────────────────────────────────────────────

    [Fact]
    public void SampleOffset_WithNonPositiveRadius_ReturnsZero()
    {
        Assert.Equal(Vector3.Zero, LightFlickerSignal.SampleOffset(1f, 3f, 0f, 0f));
        Assert.Equal(Vector3.Zero, LightFlickerSignal.SampleOffset(1f, 3f, 0f, -1f));
    }

    [Fact]
    public void SampleOffset_StaysWithinRadiusPerAxis()
    {
        var random = new Random(4);

        for (var i = 0; i < 200; i++)
        {
            var time = (float)(random.NextDouble() * 100);
            var offset = LightFlickerSignal.SampleOffset(time, 2f, 1f, 0.5f);

            Assert.InRange(offset.X, -0.5f, 0.5f);
            Assert.InRange(offset.Y, -0.5f, 0.5f);
            Assert.InRange(offset.Z, -0.5f, 0.5f);
        }
    }

    [Fact]
    public void SampleOffset_IsDeterministic()
    {
        var a = LightFlickerSignal.SampleOffset(5f, 2f, 3f, 0.2f);
        var b = LightFlickerSignal.SampleOffset(5f, 2f, 3f, 0.2f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SampleOffset_VariesOverTime()
    {
        var a = LightFlickerSignal.SampleOffset(0f, 2f, 3f, 0.2f);
        var b = LightFlickerSignal.SampleOffset(10f, 2f, 3f, 0.2f);

        Assert.NotEqual(a, b);
    }
}
