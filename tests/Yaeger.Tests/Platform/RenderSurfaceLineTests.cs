using System.Numerics;
using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class RenderSurfaceLineTests
{
    private sealed class RecordingSurface : IRenderSurface
    {
        public readonly List<(Matrix4x4 Transform, string TexturePath, Vector4 Color)> Quads = [];

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection) { }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            Quads.Add((transform, texturePath, color));

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Quads.Add((transform, texturePath, color));
    }

    [Fact]
    public void SubmitQuad_Untextured_ShouldUseSolidTexturePath()
    {
        var surface = new RecordingSurface();
        IRenderSurface s = surface;

        s.SubmitQuad(Matrix4x4.Identity, new Vector4(1, 0, 0, 1));

        var quad = Assert.Single(surface.Quads);
        Assert.Equal(IRenderSurface.SolidTexturePath, quad.TexturePath);
        Assert.Equal(new Vector4(1, 0, 0, 1), quad.Color);
    }

    [Fact]
    public void SubmitLine_Horizontal_ShouldStretchQuadBetweenEndpoints()
    {
        var surface = new RecordingSurface();
        IRenderSurface s = surface;

        s.SubmitLine(new Vector2(0, 0), new Vector2(4, 0), 0.5f, Vector4.One);

        var quad = Assert.Single(surface.Quads);
        var right = Vector3.Transform(new Vector3(0.5f, 0, 0), quad.Transform);
        var left = Vector3.Transform(new Vector3(-0.5f, 0, 0), quad.Transform);
        var top = Vector3.Transform(new Vector3(0, 0.5f, 0), quad.Transform);
        Assert.Equal(4f, right.X, 4);
        Assert.Equal(0f, left.X, 4);
        Assert.Equal(0.25f, top.Y, 4);
    }

    [Fact]
    public void CreateTransform_Diagonal_ShouldMapQuadEndsOntoSegmentEndpoints()
    {
        var from = new Vector2(1, 1);
        var to = new Vector2(4, 5);

        var transform = LineGeometry.CreateTransform(from, to, 0.1f);

        var start = Vector3.Transform(new Vector3(-0.5f, 0, 0), transform);
        var end = Vector3.Transform(new Vector3(0.5f, 0, 0), transform);
        Assert.Equal(from.X, start.X, 4);
        Assert.Equal(from.Y, start.Y, 4);
        Assert.Equal(to.X, end.X, 4);
        Assert.Equal(to.Y, end.Y, 4);
    }
}
