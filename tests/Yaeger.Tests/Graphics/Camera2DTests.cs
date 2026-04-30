using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class Camera2DTests
{
    private const float Tolerance = 1e-5f;

    private static Vector2 Project(Camera2D camera, float aspectRatio, Vector2 world)
    {
        var clip = Vector4.Transform(
            new Vector4(world, 0f, 1f),
            camera.ViewProjection(aspectRatio)
        );
        return new Vector2(clip.X, clip.Y);
    }

    [Fact]
    public void DefaultCamera_ViewProjection_MapsWorldToNdcAtUnitAspect()
    {
        // Arrange
        var camera = new Camera2D();

        // Act
        var ndc = Project(camera, aspectRatio: 1f, world: new Vector2(0.5f, 0.5f));

        // Assert
        Assert.Equal(0.5f, ndc.X, Tolerance);
        Assert.Equal(0.5f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_WithPosition_TranslatesCameraPositionToNdcOrigin()
    {
        // Arrange
        var camera = new Camera2D(new Vector2(1f, 0f));

        // Act
        var ndc = Project(camera, aspectRatio: 1f, world: new Vector2(1f, 0f));

        // Assert
        Assert.Equal(0f, ndc.X, Tolerance);
        Assert.Equal(0f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_WithZoom_ScalesWorldAppearance()
    {
        // Arrange — zoom of 2 means a world point at (0.5, 0) reaches the right edge of NDC.
        var camera = new Camera2D(Vector2.Zero, Zoom: 2f);

        // Act
        var ndc = Project(camera, aspectRatio: 1f, world: new Vector2(0.5f, 0f));

        // Assert
        Assert.Equal(1f, ndc.X, Tolerance);
        Assert.Equal(0f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_WithRotation_RotatesWorldOppositeAroundCameraPosition()
    {
        // Arrange — camera rotated +90° CCW; world (1, 0) appears at (0, -1) in view.
        var camera = new Camera2D(Vector2.Zero, Zoom: 1f, Rotation: MathF.PI / 2f);

        // Act
        var ndc = Project(camera, aspectRatio: 1f, world: new Vector2(1f, 0f));

        // Assert
        Assert.Equal(0f, ndc.X, Tolerance);
        Assert.Equal(-1f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_AtWideAspect_WorldAtAspectBoundMapsToNdcEdge()
    {
        // Arrange — at aspect 2, the visible world span is [-2, 2] × [-1, 1] when Zoom=1.
        var camera = new Camera2D();

        // Act
        var ndc = Project(camera, aspectRatio: 2f, world: new Vector2(2f, 0f));

        // Assert
        Assert.Equal(1f, ndc.X, Tolerance);
        Assert.Equal(0f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_AtWideAspect_WorldMidwayMapsToNdcMidway()
    {
        // Arrange
        var camera = new Camera2D();

        // Act
        var ndc = Project(camera, aspectRatio: 2f, world: new Vector2(1f, 0f));

        // Assert
        Assert.Equal(0.5f, ndc.X, Tolerance);
        Assert.Equal(0f, ndc.Y, Tolerance);
    }

    [Fact]
    public void Camera_CombinedPositionAndZoom_ProjectsCorrectly()
    {
        // Arrange — camera at (2, 0) with zoom 0.5; world (3, 0) is one world-unit right of camera,
        // and at zoom 0.5 one world-unit equals 0.5 NDC units.
        var camera = new Camera2D(new Vector2(2f, 0f), Zoom: 0.5f);

        // Act
        var ndc = Project(camera, aspectRatio: 1f, world: new Vector2(3f, 0f));

        // Assert
        Assert.Equal(0.5f, ndc.X, Tolerance);
        Assert.Equal(0f, ndc.Y, Tolerance);
    }
}
