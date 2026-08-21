using System.Numerics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side billboard math — no GL context needed, like TransparencySorterTests.
public class BillboardMathTests
{
    [Fact]
    public void ExtractCameraAxes_IdentityView_ReturnsWorldRightAndUp()
    {
        var (right, up) = BillboardMath.ExtractCameraAxes(Matrix4x4.Identity);

        Assert.Equal(Vector3.UnitX, right);
        Assert.Equal(Vector3.UnitY, up);
    }

    [Fact]
    public void ExtractCameraAxes_CameraOnPositiveZ_ReturnsExpectedAxes()
    {
        // Camera at +Z looking at the origin with +Y up: right is +X, up stays +Y.
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);

        var (right, up) = BillboardMath.ExtractCameraAxes(view);

        Assert.Equal(Vector3.UnitX, right, ApproxComparer);
        Assert.Equal(Vector3.UnitY, up, ApproxComparer);
    }

    [Fact]
    public void ExtractCameraAxes_CameraOnPositiveX_ReturnsExpectedAxes()
    {
        // Camera at +X looking at the origin with +Y up: right is -Z, up stays +Y.
        var view = Matrix4x4.CreateLookAt(new Vector3(5f, 0f, 0f), Vector3.Zero, Vector3.UnitY);

        var (right, up) = BillboardMath.ExtractCameraAxes(view);

        Assert.Equal(-Vector3.UnitZ, right, ApproxComparer);
        Assert.Equal(Vector3.UnitY, up, ApproxComparer);
    }

    [Fact]
    public void ExtractCameraAxes_ReturnsUnitLengthAxes()
    {
        var view = Matrix4x4.CreateLookAt(
            new Vector3(3f, 4f, 5f),
            new Vector3(1f, 0f, 0f),
            Vector3.UnitY
        );

        var (right, up) = BillboardMath.ExtractCameraAxes(view);

        Assert.Equal(1f, right.Length(), 0.0001f);
        Assert.Equal(1f, up.Length(), 0.0001f);
    }

    [Fact]
    public void ProjectVelocity_ZeroVelocity_ReturnsZeroSpeedAndRotation()
    {
        var (speed, rotation) = BillboardMath.ProjectVelocity(
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY
        );

        Assert.Equal(0f, speed);
        Assert.Equal(0f, rotation);
    }

    [Fact]
    public void ProjectVelocity_AlongCameraRight_ReturnsFullSpeedAndZeroRotation()
    {
        var (speed, rotation) = BillboardMath.ProjectVelocity(
            new Vector3(3f, 0f, 0f),
            Vector3.UnitX,
            Vector3.UnitY
        );

        Assert.Equal(3f, speed, 0.0001f);
        Assert.Equal(0f, rotation, 0.0001f);
    }

    [Fact]
    public void ProjectVelocity_AlongCameraUp_ReturnsQuarterTurnRotation()
    {
        var (speed, rotation) = BillboardMath.ProjectVelocity(
            new Vector3(0f, 3f, 0f),
            Vector3.UnitX,
            Vector3.UnitY
        );

        Assert.Equal(3f, speed, 0.0001f);
        Assert.Equal(MathF.PI / 2f, rotation, 0.0001f);
    }

    [Fact]
    public void ProjectVelocity_TowardCamera_ReturnsZeroSpeed()
    {
        // Velocity purely along the (implied) forward axis, perpendicular to both right and up,
        // projects to zero speed — a bolt flying straight at the camera should collapse back to
        // its round/square base size, not show a spurious streak.
        var (speed, rotation) = BillboardMath.ProjectVelocity(
            new Vector3(0f, 0f, 5f),
            Vector3.UnitX,
            Vector3.UnitY
        );

        Assert.Equal(0f, speed, 0.0001f);
        Assert.Equal(0f, rotation);
    }

    [Fact]
    public void ProjectVelocity_DiagonalVelocity_ReturnsCombinedSpeedAndAngle()
    {
        var (speed, rotation) = BillboardMath.ProjectVelocity(
            new Vector3(1f, 1f, 0f),
            Vector3.UnitX,
            Vector3.UnitY
        );

        Assert.Equal(MathF.Sqrt(2f), speed, 0.0001f);
        Assert.Equal(MathF.PI / 4f, rotation, 0.0001f);
    }

    // ── ComputeFrameIndex ────────────────────────────────────────────────────

    [Fact]
    public void ComputeFrameIndex_ZeroFrameRate_HoldsAtStartFrame()
    {
        Assert.Equal(2, BillboardMath.ComputeFrameIndex(2, 5f, 0f, 8));
        Assert.Equal(2, BillboardMath.ComputeFrameIndex(2, 0f, 0f, 8));
    }

    [Fact]
    public void ComputeFrameIndex_AdvancesByAgeTimesFrameRate()
    {
        // 2.5 seconds at 4 fps = 10 frames advanced.
        Assert.Equal(10 % 8, BillboardMath.ComputeFrameIndex(0, 2.5f, 4f, 8));
    }

    [Fact]
    public void ComputeFrameIndex_LoopsAcrossTheGridInsteadOfHoldingTheLastFrame()
    {
        // 4 frames at 4 fps: after 1.75s it should have wrapped back around, not clamped at 3.
        var index = BillboardMath.ComputeFrameIndex(0, 1.75f, 4f, 4);

        Assert.Equal(3, index); // floor(1.75 * 4) = 7, 7 % 4 = 3
    }

    [Fact]
    public void ComputeFrameIndex_WrapsStartFramePlusAdvanceAcrossTheGrid()
    {
        // Start on frame 3 of 4, advance by 2 frames -> wraps to frame 1.
        var index = BillboardMath.ComputeFrameIndex(3, 0.5f, 4f, 4);

        Assert.Equal(1, index);
    }

    [Fact]
    public void ComputeFrameIndex_NonFiniteAge_HoldsAtStartFrame()
    {
        Assert.Equal(1, BillboardMath.ComputeFrameIndex(1, float.NaN, 4f, 4));
        Assert.Equal(1, BillboardMath.ComputeFrameIndex(1, float.PositiveInfinity, 4f, 4));
    }

    [Fact]
    public void ComputeFrameIndex_AlwaysStaysWithinTotalFrames()
    {
        var random = new Random(11);
        for (var i = 0; i < 100; i++)
        {
            var totalFrames = random.Next(1, 20);
            var startFrame = random.Next(0, totalFrames);
            var age = (float)(random.NextDouble() * 50);
            var frameRate = (float)(random.NextDouble() * 30);

            var index = BillboardMath.ComputeFrameIndex(startFrame, age, frameRate, totalFrames);

            Assert.InRange(index, 0, totalFrames - 1);
        }
    }

    // ── GetFrameUv ───────────────────────────────────────────────────────────

    [Fact]
    public void GetFrameUv_SingleFrameGrid_CoversTheWholeTexture()
    {
        var (uvMin, uvMax) = BillboardMath.GetFrameUv(1, 1, 0);

        Assert.Equal(Vector2.Zero, uvMin);
        Assert.Equal(Vector2.One, uvMax);
    }

    [Fact]
    public void GetFrameUv_MatchesSpriteSheetGetFrameUv()
    {
        // Same grid/frame SpriteSheetTests.GetFrameUv_MultiRowSheet_UsesTopToBottomFrameIndexing
        // uses, so the two implementations must agree.
        var (uvMin, uvMax) = BillboardMath.GetFrameUv(3, 2, 3); // row 1, column 0

        Assert.Equal(0f, uvMin.X, 5);
        Assert.Equal(0f, uvMin.Y, 5);
        Assert.Equal(1f / 3f, uvMax.X, 5);
        Assert.Equal(0.5f, uvMax.Y, 5);
    }

    [Fact]
    public void GetFrameUv_OutOfRangeFrameIndex_ClampsInsteadOfThrowing()
    {
        var low = BillboardMath.GetFrameUv(2, 2, -5);
        var high = BillboardMath.GetFrameUv(2, 2, 99);

        Assert.Equal(BillboardMath.GetFrameUv(2, 2, 0), low);
        Assert.Equal(BillboardMath.GetFrameUv(2, 2, 3), high);
    }

    [Fact]
    public void GetFrameUv_NonPositiveColumnsOrRows_ClampsToOne()
    {
        var (uvMin, uvMax) = BillboardMath.GetFrameUv(0, 0, 0);

        Assert.Equal(Vector2.Zero, uvMin);
        Assert.Equal(Vector2.One, uvMax);
    }

    // ── LinearizeDepth / FadeFactor ──────────────────────────────────────────

    [Fact]
    public void LinearizeDepth_AtNearPlane_ReturnsNear()
    {
        var depth = BillboardMath.LinearizeDepth(0f, 0.1f, 100f);

        Assert.Equal(0.1f, depth, 0.001f);
    }

    [Fact]
    public void LinearizeDepth_AtFarPlane_ReturnsFar()
    {
        var depth = BillboardMath.LinearizeDepth(1f, 0.1f, 100f);

        Assert.Equal(100f, depth, 0.01f);
    }

    [Fact]
    public void LinearizeDepth_IsMonotonicallyIncreasingWithDeviceDepth()
    {
        var previous = BillboardMath.LinearizeDepth(0f, 0.1f, 100f);
        for (var i = 1; i <= 10; i++)
        {
            var current = BillboardMath.LinearizeDepth(i / 10f, 0.1f, 100f);
            Assert.True(current >= previous);
            previous = current;
        }
    }

    [Fact]
    public void FadeFactor_ZeroFadeDistance_AlwaysFullyVisible()
    {
        Assert.Equal(
            1f,
            BillboardMath.FadeFactor(sceneDepth: 1f, particleDepth: 50f, fadeDistance: 0f)
        );
        Assert.Equal(
            1f,
            BillboardMath.FadeFactor(sceneDepth: 100f, particleDepth: 1f, fadeDistance: 0f)
        );
    }

    [Fact]
    public void FadeFactor_FarBehindScene_IsFullyVisible()
    {
        var fade = BillboardMath.FadeFactor(sceneDepth: 10f, particleDepth: 1f, fadeDistance: 0.5f);

        Assert.Equal(1f, fade);
    }

    [Fact]
    public void FadeFactor_AtSceneSurface_IsFullyTransparent()
    {
        var fade = BillboardMath.FadeFactor(sceneDepth: 5f, particleDepth: 5f, fadeDistance: 0.5f);

        Assert.Equal(0f, fade);
    }

    [Fact]
    public void FadeFactor_PastSceneSurface_ClampsToFullyTransparent()
    {
        var fade = BillboardMath.FadeFactor(sceneDepth: 5f, particleDepth: 6f, fadeDistance: 0.5f);

        Assert.Equal(0f, fade);
    }

    [Fact]
    public void FadeFactor_WithinFadeBand_InterpolatesLinearly()
    {
        var fade = BillboardMath.FadeFactor(sceneDepth: 5f, particleDepth: 4.75f, fadeDistance: 1f);

        Assert.Equal(0.25f, fade, 0.0001f);
    }

    private static readonly Vector3EqualityComparer ApproxComparer = new(0.0001f);

    private sealed class Vector3EqualityComparer(float tolerance) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

        public int GetHashCode(Vector3 obj) => 0;
    }
}
