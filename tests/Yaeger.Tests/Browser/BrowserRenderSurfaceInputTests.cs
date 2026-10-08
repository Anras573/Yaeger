using System.Reflection;
using Yaeger.Browser;
using Yaeger.Input;

namespace Yaeger.Tests.Browser;

public class BrowserRenderSurfaceInputTests
{
    private static void SeedKeyDown(string code)
    {
        var field = typeof(BrowserInputState).GetField(
            "KeysDown",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        ((HashSet<string>)field.GetValue(null)!).Add(code);
    }

    [Fact]
    public void EndFrame_ShouldNotClearInputEdges()
    {
        BrowserInputState.EndFrame();
        SeedKeyDown("Space");
        var input = new BrowserInputState();
        var surface = new BrowserRenderSurface("canvas");

        surface.EndFrame();

        Assert.True(input.WasKeyPressed(Keys.Space));
        BrowserInputState.EndFrame();
        Assert.False(input.WasKeyPressed(Keys.Space));
    }
}
