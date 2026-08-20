using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class ValueNoise3DTests
{
    [Fact]
    public void Sample_StaysWithinUnitRange()
    {
        var random = new Random(1);

        for (var i = 0; i < 500; i++)
        {
            var x = (float)(random.NextDouble() * 200 - 100);
            var y = (float)(random.NextDouble() * 200 - 100);
            var z = (float)(random.NextDouble() * 200 - 100);

            var value = ValueNoise3D.Sample(x, y, z);

            Assert.InRange(value, -1f, 1f);
        }
    }

    [Fact]
    public void Sample_IsDeterministic()
    {
        var a = ValueNoise3D.Sample(3.14f, -2.7f, 9.5f);
        var b = ValueNoise3D.Sample(3.14f, -2.7f, 9.5f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Sample_IsContinuous()
    {
        // A small step in the input should produce a small step in the output — no jump between
        // lattice cells, since the fade curve interpolates continuously across them.
        var random = new Random(2);

        for (var i = 0; i < 100; i++)
        {
            var x = (float)(random.NextDouble() * 20 - 10);
            var y = (float)(random.NextDouble() * 20 - 10);
            var z = (float)(random.NextDouble() * 20 - 10);

            var a = ValueNoise3D.Sample(x, y, z);
            var b = ValueNoise3D.Sample(x + 0.0001f, y, z);

            Assert.True(MathF.Abs(a - b) < 0.01f);
        }
    }

    [Fact]
    public void Sample_IsContinuousAcrossLatticeBoundary()
    {
        // The fade curve reaches exactly 0/1 at cell edges, so sampling right at an integer
        // coordinate must agree with sampling an epsilon into the neighbouring cell.
        var fromBelow = ValueNoise3D.Sample(5f, 5f, 5f);
        var fromAbove = ValueNoise3D.Sample(5f + 1e-6f, 5f, 5f);

        Assert.True(MathF.Abs(fromBelow - fromAbove) < 0.001f);
    }

    [Fact]
    public void Sample3_ReturnsDecorrelatedAxes()
    {
        // With generic (non-degenerate) input, the three axes shouldn't all land on the same
        // value — otherwise the "shape" a per-axis turbulence displacement needs collapses to a
        // single scalar times a fixed direction.
        var result = ValueNoise3D.Sample3(new Vector3(1.23f, 4.56f, 7.89f));

        Assert.False(result.X == result.Y && result.Y == result.Z);
    }

    [Fact]
    public void Sample3_StaysWithinUnitCube()
    {
        var random = new Random(3);

        for (var i = 0; i < 200; i++)
        {
            var point = new Vector3(
                (float)(random.NextDouble() * 40 - 20),
                (float)(random.NextDouble() * 40 - 20),
                (float)(random.NextDouble() * 40 - 20)
            );

            var result = ValueNoise3D.Sample3(point);

            Assert.InRange(result.X, -1f, 1f);
            Assert.InRange(result.Y, -1f, 1f);
            Assert.InRange(result.Z, -1f, 1f);
        }
    }

    [Fact]
    public void SampleFlow_AtFixedPosition_VariesOverTime()
    {
        // A stationary point must not sample a frozen noise value forever — the field has to flow
        // past it as time advances.
        var position = new Vector3(2f, 3f, 4f);

        var atZero = ValueNoise3D.SampleFlow(position, 0f, 1f);
        var atLater = ValueNoise3D.SampleFlow(position, 5f, 1f);

        Assert.NotEqual(atZero, atLater);
    }

    [Fact]
    public void SampleFlow_IsContinuousOverTime()
    {
        var position = Vector3.Zero;

        var a = ValueNoise3D.SampleFlow(position, 10f, 1f);
        var b = ValueNoise3D.SampleFlow(position, 10.0001f, 1f);

        Assert.True(Vector3.Distance(a, b) < 0.01f);
    }

    [Fact]
    public void SampleFlow_IsDeterministic()
    {
        var position = new Vector3(1f, -1f, 2f);

        var a = ValueNoise3D.SampleFlow(position, 3f, 0.5f);
        var b = ValueNoise3D.SampleFlow(position, 3f, 0.5f);

        Assert.Equal(a, b);
    }
}
