using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class ViewportAspectTests
{
    private sealed class FakeViewport(Vector2 size) : IViewport
    {
        public Vector2 Size { get; set; } = size;
        public float PixelRatio => 1f;
    }

    private sealed class CameraRecordingSurface : IRenderSurface
    {
        public Matrix4x4? Camera;

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection) => Camera = viewProjection;

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) { }

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) { }
    }

    [Fact]
    public void AspectRatio_ShouldDefaultToWidthOverHeight()
    {
        IViewport viewport = new FakeViewport(new Vector2(800, 400));

        Assert.Equal(2f, viewport.AspectRatio);
    }

    [Fact]
    public void AspectRatio_WithZeroHeight_ShouldFallBackToOne()
    {
        IViewport viewport = new FakeViewport(new Vector2(800, 0));

        Assert.Equal(1f, viewport.AspectRatio);
    }

    [Fact]
    public void Render_ShouldBuildCameraWithViewportAspect()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        var camera = new Camera2D(Vector2.Zero);
        world.AddComponent(cameraEntity, camera);
        var surface = new CameraRecordingSurface();
        var viewport = new FakeViewport(new Vector2(800, 400));

        new UnifiedRenderSystem(surface, null, world, viewport).Render();

        Assert.Equal(camera.ViewProjection(2f), surface.Camera);
    }

    [Fact]
    public void Render_AfterViewportResize_ShouldUseNewAspect()
    {
        var world = new World();
        var cameraEntity = world.CreateEntity();
        var camera = new Camera2D(Vector2.Zero);
        world.AddComponent(cameraEntity, camera);
        var surface = new CameraRecordingSurface();
        var viewport = new FakeViewport(new Vector2(800, 400));
        var system = new UnifiedRenderSystem(surface, null, world, viewport);
        system.Render();

        viewport.Size = new Vector2(400, 400);
        system.Render();

        Assert.Equal(camera.ViewProjection(1f), surface.Camera);
    }

    [Fact]
    public void Update_WithViewport_ShouldClampCameraUsingViewportAspect()
    {
        // Bounds x:[0,10]; zoom 1 → half-width is aspect (2), so the camera clamps to x=2.
        var world = new World();
        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(-50, 0)));
        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 0f));
        world.AddComponent(
            cameraEntity,
            new CameraBounds(new Vector2(0, -100), new Vector2(10, 100))
        );

        new CameraFollowSystem(world, new FakeViewport(new Vector2(800, 400))).Update(0.016f);

        Assert.Equal(2f, world.GetComponent<Camera2D>(cameraEntity).Position.X, 0.0001f);
    }
}
