using System.Numerics;
using Yaeger.Input;
using Yaeger.Platform;

namespace Yaeger.Tests.Input;

public class MouseInsideTests
{
    private static readonly Vector2 Size = new(800, 600);

    private sealed class MinimalInputState : IInputState
    {
        public bool IsKeyPressed(Keys key) => false;

        public bool IsMouseButtonPressed(MouseButton button) => false;

        public bool WasKeyPressed(Keys key) => false;

        public bool WasKeyReleased(Keys key) => false;

        public bool WasMouseButtonPressed(MouseButton button) => false;

        public bool WasMouseButtonReleased(MouseButton button) => false;

        public Vector2 MousePosition => Vector2.Zero;
        public Vector2 MousePositionNdc => Vector2.Zero;
        public float ScrollDelta => 0f;
    }

    [Fact]
    public void ComputeIsInside_BeforeAnyMove_ShouldBeFalse()
    {
        Assert.False(Mouse.ComputeIsInside(false, Vector2.Zero, Size));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(799, 599, true)]
    [InlineData(800, 300, false)]
    [InlineData(300, 600, false)]
    [InlineData(-1, 10, false)]
    [InlineData(10, -1, false)]
    public void ComputeIsInside_AfterMove_ShouldCompareAgainstWindowSize(
        float x,
        float y,
        bool expected
    )
    {
        Assert.Equal(expected, Mouse.ComputeIsInside(true, new Vector2(x, y), Size));
    }

    [Fact]
    public void IsMouseInside_DefaultInterfaceMember_ShouldBeTrue()
    {
        IInputState state = new MinimalInputState();

        Assert.True(state.IsMouseInside);
    }
}
