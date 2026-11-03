using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class UnifiedRenderSystemTextSpaceTests
{
    private sealed class Viewport : IViewport
    {
        public Vector2 Size => new(800, 600);
        public float PixelRatio => 1f;
    }

    private sealed class RecordingSurface : IRenderSurface, ITextRenderSurface
    {
        public Matrix4x4 Camera = Matrix4x4.Identity;
        public readonly List<string> Log = [];
        public Matrix4x4? CameraAtText;

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection)
        {
            Camera = viewProjection;
            Log.Add("camera");
        }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            Log.Add("quad");

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Log.Add("quad");

        public void DrawText(
            string text,
            Matrix4x4 transform,
            FontHandle font,
            int fontSize,
            Color color
        )
        {
            CameraAtText = Camera;
            Log.Add("text");
        }

        public void DrawText(
            string text,
            Matrix4x4 transform,
            IFontHandle font,
            int fontSize,
            Color color
        ) => DrawText(text, transform, default(FontHandle), fontSize, color);
    }

    [Fact]
    public void Render_TextWithRotatedCamera_DrawsUnderIdentityCameraAndRestoresIt()
    {
        var world = new World();
        var cam = world.CreateEntity();
        var camera = new Camera2D(Vector2.Zero, 1f, MathF.PI / 2);
        world.AddComponent(cam, camera);
        var sprite = world.CreateEntity();
        world.AddComponent(sprite, new Transform2D(Vector2.Zero));
        world.AddComponent(sprite, new Sprite("a.png"));
        var label = world.CreateEntity();
        world.AddComponent(label, new Transform2D(new Vector2(-0.9f, 0.9f)));
        world.AddComponent(label, new RenderLayer(1));
        world.AddComponent(label, new Text("HUD", new FontHandle("f.ttf"), 12, Color.White));

        var surface = new RecordingSurface();
        new UnifiedRenderSystem(surface, surface, world, new Viewport()).Render();

        Assert.Equal(Matrix4x4.Identity, surface.CameraAtText);
        Assert.Equal(camera.ViewProjection(800f / 600f), surface.Camera);
    }
}
