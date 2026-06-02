using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class Camera3DTests
{
    private const float Tolerance = 1e-5f;
    private const float AspectRatio = 16f / 9f;

    [Fact]
    public void ParameterlessCtor_ProducesSafeDefaults()
    {
        // Arrange / Act
        var camera = new Camera3D();

        // Assert — FOV and Near must be > 0 for CreatePerspectiveFieldOfView to succeed
        Assert.True(camera.Fov > 0f);
        Assert.True(camera.Near > 0f);
        Assert.True(camera.Far > camera.Near);
    }

    [Fact]
    public void ParameterlessCtor_MatchesDefault()
    {
        var a = new Camera3D();
        var b = Camera3D.Default;

        Assert.Equal(b.Position, a.Position);
        Assert.Equal(b.Target, a.Target);
        Assert.Equal(b.Up, a.Up);
        Assert.Equal(b.Fov, a.Fov);
        Assert.Equal(b.Near, a.Near);
        Assert.Equal(b.Far, a.Far);
    }

    [Fact]
    public void ViewMatrix_TargetOnNegativeZ_LooksDownNegativeZ()
    {
        // Camera at origin looking at (0,0,-1): forward is -Z, so the view matrix maps
        // a point at (0,0,-1) world to (0,0,positive) in view space.
        var camera = new Camera3D(
            Vector3.Zero,
            new Vector3(0, 0, -1),
            Vector3.UnitY,
            MathF.PI / 2,
            0.1f,
            100f
        );

        var viewPoint = Vector4.Transform(new Vector4(0, 0, -1, 1), camera.ViewMatrix);

        Assert.Equal(0f, viewPoint.X, Tolerance);
        Assert.Equal(0f, viewPoint.Y, Tolerance);
        Assert.True(viewPoint.Z > 0f); // in front of camera (+Z is behind viewer in OpenGL)
    }

    [Fact]
    public void ProjectionMatrix_DoesNotThrow_WithDefaultValues()
    {
        var camera = Camera3D.Default;
        var exception = Record.Exception(() => camera.ProjectionMatrix(AspectRatio));
        Assert.Null(exception);
    }

    [Fact]
    public void ViewProjection_ProjectsTargetToNdcOriginXY()
    {
        // The camera target, projected through view+projection, should land at NDC (0, 0).
        var camera = Camera3D.Default;
        var clip = Vector4.Transform(
            new Vector4(camera.Target, 1f),
            camera.ViewProjection(AspectRatio)
        );
        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;

        Assert.Equal(0f, ndcX, Tolerance);
        Assert.Equal(0f, ndcY, Tolerance);
    }

    [Fact]
    public void ViewProjection_MatchesManualMultiplication()
    {
        // ViewProjection(a) must equal ViewMatrix * ProjectionMatrix(a).
        var camera = Camera3D.Default;
        var combined = camera.ViewProjection(AspectRatio);
        var manual = camera.ViewMatrix * camera.ProjectionMatrix(AspectRatio);

        // Sample a handful of elements to confirm identical computation.
        Assert.Equal(manual.M11, combined.M11, Tolerance);
        Assert.Equal(manual.M22, combined.M22, Tolerance);
        Assert.Equal(manual.M33, combined.M33, Tolerance);
        Assert.Equal(manual.M44, combined.M44, Tolerance);
        Assert.Equal(manual.M34, combined.M34, Tolerance);
        Assert.Equal(manual.M43, combined.M43, Tolerance);
    }

    [Fact]
    public void Default_HasExpectedFieldValues()
    {
        var camera = Camera3D.Default;

        Assert.Equal(new Vector3(0, 2, 5), camera.Position);
        Assert.Equal(Vector3.Zero, camera.Target);
        Assert.Equal(Vector3.UnitY, camera.Up);
        Assert.Equal(MathF.PI / 4, camera.Fov, Tolerance);
        Assert.Equal(0.1f, camera.Near, Tolerance);
        Assert.Equal(1000f, camera.Far, Tolerance);
    }
}
