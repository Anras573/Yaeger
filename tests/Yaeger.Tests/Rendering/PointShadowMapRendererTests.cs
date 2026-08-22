using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side face-capture/caster-selection maths — no GL context needed, like
// ShadowMapRendererTests' ComputeLightSpaceMatrix coverage.
public class PointShadowMapRendererTests
{
    private static Vector3 ProjectToNdc(Vector3 worldPoint, Matrix4x4 viewProj)
    {
        var clip = Vector4.Transform(new Vector4(worldPoint, 1f), viewProj);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    // ── ComputeFaceViewProjection ────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void ComputeFaceViewProjection_OutOfRangeFace_Throws(int faceIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PointShadowMapRenderer.ComputeFaceViewProjection(faceIndex, Vector3.Zero, 0.05f, 10f)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ComputeFaceViewProjection_PointAlongFaceDirection_ProjectsNearNdcCenter(int face)
    {
        var lightPos = new Vector3(3f, 1f, -2f);
        var matrix = PointShadowMapRenderer.ComputeFaceViewProjection(face, lightPos, 0.05f, 10f);
        var (target, _) = IblPrefilter.FaceDirections[face];

        var ndc = ProjectToNdc(lightPos + target * 2f, matrix);

        Assert.Equal(0f, ndc.X, precision: 3);
        Assert.Equal(0f, ndc.Y, precision: 3);
        Assert.InRange(ndc.Z, -1f, 1f);
    }

    [Fact]
    public void ComputeFaceViewProjection_TranslatesWithTheLightPosition()
    {
        var (target, _) = IblPrefilter.FaceDirections[0];
        var origin = PointShadowMapRenderer.ComputeFaceViewProjection(0, Vector3.Zero, 0.05f, 10f);
        var offset = new Vector3(5f, 2f, -1f);
        var moved = PointShadowMapRenderer.ComputeFaceViewProjection(0, offset, 0.05f, 10f);

        var ndcAtOrigin = ProjectToNdc(target * 2f, origin);
        var ndcAtMoved = ProjectToNdc(offset + target * 2f, moved);

        // The same point relative to each light's own position should land at the same NDC
        // coordinates — the capture is light-relative, so moving the light moves the frustum with it.
        Assert.Equal(ndcAtOrigin.X, ndcAtMoved.X, precision: 3);
        Assert.Equal(ndcAtOrigin.Y, ndcAtMoved.Y, precision: 3);
        Assert.Equal(ndcAtOrigin.Z, ndcAtMoved.Z, precision: 3);
    }

    [Fact]
    public void ComputeFaceViewProjection_NonPositiveNearPlane_FallsBackToASafeDefault()
    {
        var matrix = PointShadowMapRenderer.ComputeFaceViewProjection(0, Vector3.Zero, -1f, 10f);
        var (target, _) = IblPrefilter.FaceDirections[0];

        var ndc = ProjectToNdc(target * 1f, matrix);

        Assert.True(float.IsFinite(ndc.Z));
    }

    [Fact]
    public void ComputeFaceViewProjection_FarNotGreaterThanNear_FallsBackToASafeRange()
    {
        var matrix = PointShadowMapRenderer.ComputeFaceViewProjection(0, Vector3.Zero, 5f, 1f);

        Assert.True(float.IsFinite(matrix.M11));
        Assert.True(float.IsFinite(matrix.M33));
        Assert.True(float.IsFinite(matrix.M43));
    }

    // ── SelectShadowCasters ───────────────────────────────────────────────────

    private static (Vector3 Position, PointLight Light) Caster(float x, bool castsShadows = true) =>
        (new Vector3(x, 0f, 0f), PointLight.Default with { CastsShadows = castsShadows });

    [Fact]
    public void SelectShadowCasters_NoCastersFlagged_ReturnsZero()
    {
        ReadOnlySpan<(Vector3, PointLight)> lights =
        [
            Caster(1f, castsShadows: false),
            Caster(2f, castsShadows: false),
        ];
        Span<int> destination = stackalloc int[2];

        var count = PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);

        Assert.Equal(0, count);
    }

    [Fact]
    public void SelectShadowCasters_FewerThanCap_ReturnsAllOfThem()
    {
        ReadOnlySpan<(Vector3, PointLight)> lights = [Caster(1f)];
        Span<int> destination = stackalloc int[2];

        var count = PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);

        Assert.Equal(1, count);
        Assert.Equal(0, destination[0]);
    }

    [Fact]
    public void SelectShadowCasters_MoreThanCap_PicksTheClosestToTheCamera()
    {
        // Distances from the camera at the origin: index 0 -> 5, index 1 -> 1, index 2 -> 3,
        // index 3 -> 2, index 4 -> 4. The two closest are indices 1 (dist 1) and 3 (dist 2).
        ReadOnlySpan<(Vector3, PointLight)> lights =
        [
            Caster(5f),
            Caster(1f),
            Caster(3f),
            Caster(2f),
            Caster(4f),
        ];
        Span<int> destination = stackalloc int[2];

        var count = PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);

        Assert.Equal(2, count);
        Assert.Equal(1, destination[0]);
        Assert.Equal(3, destination[1]);
    }

    [Fact]
    public void SelectShadowCasters_IgnoresLightsWithoutCastsShadowsRegardlessOfDistance()
    {
        ReadOnlySpan<(Vector3, PointLight)> lights =
        [
            Caster(0.1f, castsShadows: false), // closest, but not flagged
            Caster(5f),
        ];
        Span<int> destination = stackalloc int[2];

        var count = PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);

        Assert.Equal(1, count);
        Assert.Equal(1, destination[0]);
    }

    [Fact]
    public void SelectShadowCasters_EmptyLights_ReturnsZeroWithoutThrowing()
    {
        Span<int> destination = stackalloc int[2];

        var count = PointShadowMapRenderer.SelectShadowCasters(
            ReadOnlySpan<(Vector3, PointLight)>.Empty,
            Vector3.Zero,
            destination
        );

        Assert.Equal(0, count);
    }

    [Fact]
    public void SelectShadowCasters_EmptyDestination_ReturnsZeroWithoutThrowing()
    {
        ReadOnlySpan<(Vector3, PointLight)> lights = [Caster(1f)];

        var count = PointShadowMapRenderer.SelectShadowCasters(
            lights,
            Vector3.Zero,
            Span<int>.Empty
        );

        Assert.Equal(0, count);
    }

    [Fact]
    public void SelectShadowCasters_IsDeterministicForEqualDistances()
    {
        ReadOnlySpan<(Vector3, PointLight)> lights = [Caster(2f), Caster(-2f), Caster(2f)];
        Span<int> destination = stackalloc int[2];

        var a = new int[2];
        var b = new int[2];
        PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);
        destination.CopyTo(a);
        PointShadowMapRenderer.SelectShadowCasters(lights, Vector3.Zero, destination);
        destination.CopyTo(b);

        Assert.Equal(a, b);
    }
}
