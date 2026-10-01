using System.Numerics;
using Yaeger.Browser;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Tests.Browser;

public class BrowserTextRenderSurfaceTests
{
    private sealed class FakeMetrics : IGlyphMetricsProvider
    {
        public FontLineMetrics GetLineMetrics(string fontKey, int fontSize) => new(20f, 16f);

        public bool TryGetGlyph(string fontKey, int fontSize, int codepoint, out GlyphMetrics glyph)
        {
            glyph = new GlyphMetrics(
                10f,
                1f,
                -2f,
                8f,
                12f,
                new Vector2(0.25f, 0.5f),
                new Vector2(0.5f, 0.75f),
                "font://test/0"
            );
            return true;
        }
    }

    private sealed class RecordingSurface : IRenderSurface
    {
        public List<(
            Matrix4x4 Transform,
            string Path,
            Vector2 UvMin,
            Vector2 UvMax,
            Vector4 Color
        )> Quads { get; } = [];

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() { }

        public void SetCamera(Matrix4x4 viewProjection) { }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            SubmitQuad(transform, texturePath, Vector2.Zero, Vector2.One, color);

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Quads.Add((transform, texturePath, uvMin, uvMax, color));
    }

    [Fact]
    public void DrawText_ShouldSubmitOneAtlasQuadPerGlyph()
    {
        var surface = new RecordingSurface();
        var text = new BrowserTextRenderSurface(surface, new FakeMetrics());

        text.DrawText("ab", Matrix4x4.Identity, new FontHandle("sans"), 16, new Color(255, 0, 0));

        Assert.Equal(2, surface.Quads.Count);
        Assert.All(surface.Quads, q => Assert.Equal("font://test/0", q.Path));
        Assert.Equal(new Vector2(0.25f, 0.5f), surface.Quads[0].UvMin);
        Assert.Equal(new Vector4(1f, 0f, 0f, 1f), surface.Quads[0].Color);
    }

    [Fact]
    public void DrawText_ShouldPlaceQuadFromGlyphRectThroughTransform()
    {
        var surface = new RecordingSurface();
        var text = new BrowserTextRenderSurface(surface, new FakeMetrics());

        text.DrawText(
            "a",
            Matrix4x4.CreateScale(2f),
            new FontHandle("sans"),
            16,
            new Color(255, 255, 255)
        );

        // Glyph rect: x 1..9, y -2..10 -> unit-quad centre (5, 4), size (8, 12), then scaled by 2.
        var q = surface.Quads[0].Transform;
        var centre = Vector3.Transform(Vector3.Zero, q);
        var corner = Vector3.Transform(new Vector3(0.5f, 0.5f, 0f), q);
        Assert.Equal(10f, centre.X, 4);
        Assert.Equal(8f, centre.Y, 4);
        Assert.Equal(18f, corner.X, 4);
        Assert.Equal(20f, corner.Y, 4);
    }

    [Fact]
    public void DrawText_EmptyText_ShouldSubmitNothing()
    {
        var surface = new RecordingSurface();
        var text = new BrowserTextRenderSurface(surface, new FakeMetrics());

        text.DrawText("", Matrix4x4.Identity, new FontHandle("sans"), 16, new Color(255, 255, 255));

        Assert.Empty(surface.Quads);
    }

    [Fact]
    public void DrawText_NonPositiveSize_ShouldThrow()
    {
        var text = new BrowserTextRenderSurface(new RecordingSurface(), new FakeMetrics());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            text.DrawText(
                "a",
                Matrix4x4.Identity,
                new FontHandle("sans"),
                0,
                new Color(255, 255, 255)
            )
        );
    }
}
