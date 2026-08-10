using System.Numerics;
using Yaeger.Audio;

namespace Yaeger.Tests.Audio;

// Pure CPU-side math — no OpenAL device needed, unlike the rest of Audio/.
public class AudioSpatialMathTests
{
    [Fact]
    public void ExtractOrientation_LookingDownNegativeZ_ShouldReturnStandardOrientation()
    {
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY);

        var (at, up) = AudioSpatialMath.ExtractOrientation(view);

        AssertApproximatelyEqual(-Vector3.UnitZ, at);
        AssertApproximatelyEqual(Vector3.UnitY, up);
    }

    [Fact]
    public void ExtractOrientation_LookingDownPositiveX_ShouldReturnRotatedAt()
    {
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitX, Vector3.UnitY);

        var (at, up) = AudioSpatialMath.ExtractOrientation(view);

        AssertApproximatelyEqual(Vector3.UnitX, at);
        AssertApproximatelyEqual(Vector3.UnitY, up);
    }

    [Fact]
    public void ExtractOrientation_OffsetCameraLookingAtOrigin_ShouldPointTowardsTarget()
    {
        var position = new Vector3(0f, 2f, 5f);
        var target = Vector3.Zero;
        var view = Matrix4x4.CreateLookAt(position, target, Vector3.UnitY);

        var (at, _) = AudioSpatialMath.ExtractOrientation(view);

        AssertApproximatelyEqual(Vector3.Normalize(target - position), at);
    }

    [Fact]
    public void ExtractOrientation_TiltedUpVector_ShouldReturnMatchingUp()
    {
        // Camera looking along +Y with "up" pointing along -Z (e.g. a top-down view).
        var view = Matrix4x4.CreateLookAt(Vector3.Zero, Vector3.UnitY, -Vector3.UnitZ);

        var (at, up) = AudioSpatialMath.ExtractOrientation(view);

        AssertApproximatelyEqual(Vector3.UnitY, at);
        AssertApproximatelyEqual(-Vector3.UnitZ, up);
    }

    [Fact]
    public void ExtractOrientation_ReturnsNormalizedVectors()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(3f, 4f, 0f), Vector3.Zero, Vector3.UnitY);

        var (at, up) = AudioSpatialMath.ExtractOrientation(view);

        Assert.Equal(1f, at.Length(), precision: 5);
        Assert.Equal(1f, up.Length(), precision: 5);
    }

    [Fact]
    public void ExtractOrientation_IdentityMatrix_ShouldMatchPlaneOrientation()
    {
        // Camera3D.ViewMatrix returns Identity for degenerate inputs (e.g. Position == Target).
        // Identity's own backward/up rows happen to already decode to the same default-forward
        // convention as PlaneOrientation, so this is what AudioSystem's listener resolves to for
        // a degenerate Camera3D rather than producing NaN.
        var (at, up) = AudioSpatialMath.ExtractOrientation(Matrix4x4.Identity);

        Assert.Equal(AudioSpatialMath.PlaneOrientation.At, at);
        Assert.Equal(AudioSpatialMath.PlaneOrientation.Up, up);
    }

    [Fact]
    public void ExtractOrientation_ZeroMatrix_ShouldFallBackToPlaneOrientation()
    {
        // A genuinely degenerate matrix (zero-length rows) must not divide by zero / produce NaN.
        var (at, up) = AudioSpatialMath.ExtractOrientation(default);

        Assert.Equal(AudioSpatialMath.PlaneOrientation.At, at);
        Assert.Equal(AudioSpatialMath.PlaneOrientation.Up, up);
    }

    [Fact]
    public void ToListenerPlane_ShouldMapXYToZeroZPlane()
    {
        var result = AudioSpatialMath.ToListenerPlane(new Vector2(3f, -7f));

        Assert.Equal(new Vector3(3f, -7f, 0f), result);
    }

    [Fact]
    public void PlaneOrientation_ShouldFaceIntoScreenWithYUp()
    {
        Assert.Equal(-Vector3.UnitZ, AudioSpatialMath.PlaneOrientation.At);
        Assert.Equal(Vector3.UnitY, AudioSpatialMath.PlaneOrientation.Up);
    }

    private static void AssertApproximatelyEqual(
        Vector3 expected,
        Vector3 actual,
        float tolerance = 1e-4f
    )
    {
        Assert.True(
            Vector3.Distance(expected, actual) < tolerance,
            $"Expected {expected}, got {actual}"
        );
    }
}
