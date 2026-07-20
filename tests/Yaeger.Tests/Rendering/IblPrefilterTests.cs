using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Exercises the GL-free capture-direction/mip-selection maths. Constructing an IblPrefilter needs
// a live OpenGL context, so only the static helpers are unit-tested (mirroring the
// renderer/window test convention in CLAUDE.md, same as ShadowMapRendererTests).
public class IblPrefilterTests
{
    private static Vector3 ProjectToNdc(Vector3 worldPoint, Matrix4x4 viewProj)
    {
        // Matches the GL convention used throughout the engine: clip = worldPoint(row) * matrix,
        // which Vector4.Transform computes directly.
        var clip = Vector4.Transform(new Vector4(worldPoint, 1f), viewProj);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    // ── FaceDirections ──────────────────────────────────────────────────────

    [Fact]
    public void FaceDirections_HasSixEntries()
    {
        Assert.Equal(6, IblPrefilter.FaceDirections.Length);
    }

    [Theory]
    [InlineData(0, 1f, 0f, 0f)] // +X right
    [InlineData(1, -1f, 0f, 0f)] // -X left
    [InlineData(2, 0f, 1f, 0f)] // +Y top
    [InlineData(3, 0f, -1f, 0f)] // -Y bottom
    [InlineData(4, 0f, 0f, 1f)] // +Z front
    [InlineData(5, 0f, 0f, -1f)] // -Z back
    public void FaceDirections_TargetMatchesCubemapFaceOrder(int face, float x, float y, float z)
    {
        Assert.Equal(new Vector3(x, y, z), IblPrefilter.FaceDirections[face].Target);
    }

    [Fact]
    public void FaceDirections_EveryTargetAndUpAreUnitLengthAndOrthogonal()
    {
        foreach (var (target, up) in IblPrefilter.FaceDirections)
        {
            Assert.Equal(1f, target.Length(), 4);
            Assert.Equal(1f, up.Length(), 4);
            Assert.Equal(0f, Vector3.Dot(target, up), 4);
        }
    }

    // ── CaptureViewProjection ────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void CaptureViewProjection_OutOfRangeFace_Throws(int faceIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IblPrefilter.CaptureViewProjection(faceIndex)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CaptureViewProjection_FaceTargetDirectionProjectsToScreenCenter(int face)
    {
        var viewProj = IblPrefilter.CaptureViewProjection(face);
        var target = IblPrefilter.FaceDirections[face].Target;

        // A point straight down the capture direction, well inside the near/far range, should
        // land at the centre of NDC space.
        var ndc = ProjectToNdc(target * 2f, viewProj);

        Assert.Equal(0f, ndc.X, 3);
        Assert.Equal(0f, ndc.Y, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void CaptureViewProjection_FartherAlongTargetHasLargerDepth(int face)
    {
        var viewProj = IblPrefilter.CaptureViewProjection(face, near: 0.1f, far: 10f);
        var target = IblPrefilter.FaceDirections[face].Target;

        var nearerDepth = ProjectToNdc(target * 1f, viewProj).Z;
        var fartherDepth = ProjectToNdc(target * 5f, viewProj).Z;

        Assert.True(fartherDepth > nearerDepth);
    }

    // ── RoughnessForMip ──────────────────────────────────────────────────────

    [Fact]
    public void RoughnessForMip_FirstMip_IsZero()
    {
        Assert.Equal(0f, IblPrefilter.RoughnessForMip(0, 5));
    }

    [Fact]
    public void RoughnessForMip_LastMip_IsOne()
    {
        Assert.Equal(1f, IblPrefilter.RoughnessForMip(4, 5));
    }

    [Fact]
    public void RoughnessForMip_MiddleMip_InterpolatesLinearly()
    {
        Assert.Equal(0.5f, IblPrefilter.RoughnessForMip(2, 5), 4);
    }

    [Fact]
    public void RoughnessForMip_SingleLevelChain_AlwaysZero()
    {
        Assert.Equal(0f, IblPrefilter.RoughnessForMip(0, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void RoughnessForMip_OutOfRangeMip_Clamps(int mip)
    {
        var roughness = IblPrefilter.RoughnessForMip(mip, 5);
        Assert.InRange(roughness, 0f, 1f);
    }

    // ── MipResolution ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(128, 0, 128)]
    [InlineData(128, 1, 64)]
    [InlineData(128, 2, 32)]
    [InlineData(128, 7, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 5, 1)]
    public void MipResolution_HalvesPerLevelClampedToOne(int baseResolution, int mip, int expected)
    {
        Assert.Equal(expected, IblPrefilter.MipResolution(baseResolution, mip));
    }

    [Fact]
    public void MipResolution_NegativeMip_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IblPrefilter.MipResolution(128, -1));
    }
}
