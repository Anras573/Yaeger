using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class CameraFrustumTests
{
    // Camera at (0,0,10) looking at origin; near=1, far=200; 90° FOV.
    // In world space: near plane at z≈9, far plane at z≈-190.
    private static Camera3D TestCamera =>
        new(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, MathF.PI / 2, 1f, 200f);

    private static CameraFrustum BuildFrustum() =>
        CameraFrustum.FromMatrix(TestCamera.ViewProjection(16f / 9f));

    [Fact]
    public void Intersects_BoxAtOrigin_ShouldBeInside()
    {
        // Arrange — origin is well inside the frustum (distance 10 from camera, near=1, far=200)
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Intersects_BoxBehindCamera_ShouldBeOutside()
    {
        // Arrange — box at z=15..17 is behind the camera (camera at z=10, looking toward -Z)
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, -1, 15), new Vector3(1, 1, 17));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Intersects_BoxBeyondFarPlane_ShouldBeOutside()
    {
        // Arrange — box at z=-210..-200 is past the far plane (far=200 units from camera)
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, -1, -210), new Vector3(1, 1, -200));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Intersects_BoxFarToTheRight_ShouldBeOutside()
    {
        // Arrange — 90° vertical FOV + 16:9 aspect → half-width ≈ 17.8 units at distance 10.
        // A box starting at x=200 is well outside the right side.
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(200, -1, -1), new Vector3(201, 1, 1));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Intersects_BoxFarAbove_ShouldBeOutside()
    {
        // Arrange — box 200 units above the frustum
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, 200, -1), new Vector3(1, 201, 1));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Intersects_LargeBoxEnclosingCamera_ShouldBeInside()
    {
        // Arrange — a huge AABB that contains the entire scene including the camera
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000));

        // Act
        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Intersects_BoxWithModelTranslationIntoFrustum_ShouldBeInside()
    {
        // Arrange — AABB defined at local origin; model matrix moves it to (5,0,-3),
        // which is inside the frustum (camera at z=10 looking toward z=0).
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var model = Matrix4x4.CreateTranslation(5, 0, -3);

        // Act
        var result = frustum.Intersects(aabb, model);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Intersects_BoxWithModelTranslationOutOfFrustum_ShouldBeOutside()
    {
        // Arrange — AABB at local origin, translated 500 units to the right via model matrix
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
        var model = Matrix4x4.CreateTranslation(500, 0, 0);

        // Act
        var result = frustum.Intersects(aabb, model);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void FromMatrix_DefaultCamera_DoesNotThrow()
    {
        // Arrange
        var camera = Camera3D.Default;
        var viewProj = camera.ViewProjection(16f / 9f);

        // Act & Assert — must not throw with a valid matrix
        var exception = Record.Exception(() => CameraFrustum.FromMatrix(viewProj));
        Assert.Null(exception);
    }

    [Fact]
    public void Intersects_BoxInOpenGlClipRange_ShouldBeInside()
    {
        // Regression guard: System.Numerics' perspective matrix maps Camera3D.Near to
        // NDC_z = 0, but OpenGL clips at NDC_z = -1 (no glClipControl in the renderer).
        // Objects with NDC_z in [-1, 0) are visible to OpenGL even though clip_z < 0.
        // Using col(3) alone would incorrectly reject them; the correct near plane is
        // col(3) + col(4), i.e. z_clip + w_clip >= 0 (NDC_z >= -1).
        // This box sits at world_z ≈ 9.3 (camera at z=10), giving NDC_z ≈ -0.43 —
        // inside OpenGL's clip range and must not be culled.
        var frustum = BuildFrustum();
        var aabb = new Aabb3D(new Vector3(-0.5f, -0.5f, 9.2f), new Vector3(0.5f, 0.5f, 9.4f));

        var result = frustum.Intersects(aabb, Matrix4x4.Identity);

        Assert.True(result);
    }
}
