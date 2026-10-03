using System.Numerics;
using Yaeger.Browser;

namespace Yaeger.Tests.Browser;

public class BrowserRenderSurfaceClearColorTests
{
    [Fact]
    public void ClearColor_ShouldDefaultToOpaqueBlack()
    {
        var surface = new BrowserRenderSurface("canvas");

        Assert.Equal(new Vector4(0f, 0f, 0f, 1f), surface.ClearColor);
    }

    [Fact]
    public void ClearFrame_ShouldForwardConfiguredClearColor()
    {
        var surface = new BrowserRenderSurface("canvas");
        var forwarded = new List<Vector4>();
        surface.ClearFrameSink = forwarded.Add;

        surface.ClearColor = new Vector4(0.4f, 0.7f, 1f, 1f);
        surface.ClearFrame();

        Assert.Equal([new Vector4(0.4f, 0.7f, 1f, 1f)], forwarded);
    }

    [Fact]
    public void ClearFrame_ShouldForwardUpdatedColorOnLaterFrames()
    {
        var surface = new BrowserRenderSurface("canvas");
        var forwarded = new List<Vector4>();
        surface.ClearFrameSink = forwarded.Add;

        surface.ClearFrame();
        surface.ClearColor = new Vector4(0f, 0f, 0f, 0f);
        surface.ClearFrame();

        Assert.Equal([new Vector4(0f, 0f, 0f, 1f), Vector4.Zero], forwarded);
    }
}
