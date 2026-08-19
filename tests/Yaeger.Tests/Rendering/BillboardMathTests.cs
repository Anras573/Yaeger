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

    private static readonly Vector3EqualityComparer ApproxComparer = new(0.0001f);

    private sealed class Vector3EqualityComparer(float tolerance) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

        public int GetHashCode(Vector3 obj) => 0;
    }
}
