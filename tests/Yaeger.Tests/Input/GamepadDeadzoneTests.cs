using System.Numerics;
using Yaeger.Input;

namespace Yaeger.Tests.Input;

public class GamepadDeadzoneTests
{
    [Fact]
    public void ApplyDeadzone_MagnitudeBelowDeadzone_ShouldReturnZero()
    {
        var result = Gamepad.ApplyDeadzone(new Vector2(0.1f, 0f), deadzone: 0.15f);

        Assert.Equal(Vector2.Zero, result);
    }

    [Fact]
    public void ApplyDeadzone_MagnitudeExactlyAtDeadzone_ShouldReturnZero()
    {
        var result = Gamepad.ApplyDeadzone(new Vector2(0.15f, 0f), deadzone: 0.15f);

        Assert.Equal(Vector2.Zero, result);
    }

    [Fact]
    public void ApplyDeadzone_FullMagnitude_ShouldReturnFullMagnitude()
    {
        var result = Gamepad.ApplyDeadzone(new Vector2(1f, 0f), deadzone: 0.15f);

        Assert.Equal(1f, result.X, 0.0001f);
        Assert.Equal(0f, result.Y, 0.0001f);
    }

    [Fact]
    public void ApplyDeadzone_MidRange_ShouldRescaleFromDeadzoneEdgeToFullMagnitude()
    {
        var result = Gamepad.ApplyDeadzone(new Vector2(0.5f, 0f), deadzone: 0.15f);

        // (0.5 - 0.15) / (1 - 0.15) = 0.41176...
        Assert.Equal(0.41176f, result.X, 0.001f);
        Assert.Equal(0f, result.Y, 0.0001f);
    }

    [Fact]
    public void ApplyDeadzone_ShouldPreserveDirection()
    {
        var raw = new Vector2(0.6f, 0.8f); // magnitude exactly 1

        var result = Gamepad.ApplyDeadzone(raw, deadzone: 0.15f);

        Assert.Equal(raw.X, result.X, 0.0001f);
        Assert.Equal(raw.Y, result.Y, 0.0001f);
    }

    [Fact]
    public void ApplyDeadzone_ZeroInput_ShouldReturnZero()
    {
        var result = Gamepad.ApplyDeadzone(Vector2.Zero, deadzone: 0.15f);

        Assert.Equal(Vector2.Zero, result);
    }

    [Fact]
    public void ApplyDeadzone_ZeroDeadzone_ShouldReturnInputUnchanged()
    {
        var raw = new Vector2(0.3f, 0.4f);

        var result = Gamepad.ApplyDeadzone(raw, deadzone: 0f);

        Assert.Equal(raw.X, result.X, 0.0001f);
        Assert.Equal(raw.Y, result.Y, 0.0001f);
    }

    [Fact]
    public void ApplyDeadzone_DeadzoneAtOrBeyondUnitCircle_ShouldAlwaysReturnZero()
    {
        var result = Gamepad.ApplyDeadzone(new Vector2(1f, 0f), deadzone: 1.5f);

        Assert.Equal(Vector2.Zero, result);
    }

    [Fact]
    public void Deadzone_ShouldDefaultToPointOneFive()
    {
        Assert.Equal(0.15f, Gamepad.Deadzone);
    }

    [Fact]
    public void Deadzone_ShouldBeConfigurable()
    {
        var original = Gamepad.Deadzone;
        try
        {
            Gamepad.Deadzone = 0.3f;
            Assert.Equal(0.3f, Gamepad.Deadzone);
        }
        finally
        {
            Gamepad.Deadzone = original;
        }
    }
}
