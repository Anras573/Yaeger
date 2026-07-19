using Silk.NET.Input;
using Yaeger.Input;

namespace Yaeger.Tests.Input;

public class GamepadButtonMapperTests
{
    [Theory]
    [InlineData(ButtonName.A, GamepadButton.A)]
    [InlineData(ButtonName.B, GamepadButton.B)]
    [InlineData(ButtonName.X, GamepadButton.X)]
    [InlineData(ButtonName.Y, GamepadButton.Y)]
    [InlineData(ButtonName.LeftBumper, GamepadButton.LeftBumper)]
    [InlineData(ButtonName.RightBumper, GamepadButton.RightBumper)]
    [InlineData(ButtonName.Back, GamepadButton.Back)]
    [InlineData(ButtonName.Start, GamepadButton.Start)]
    [InlineData(ButtonName.Home, GamepadButton.Home)]
    [InlineData(ButtonName.LeftStick, GamepadButton.LeftStickButton)]
    [InlineData(ButtonName.RightStick, GamepadButton.RightStickButton)]
    [InlineData(ButtonName.DPadUp, GamepadButton.DPadUp)]
    [InlineData(ButtonName.DPadRight, GamepadButton.DPadRight)]
    [InlineData(ButtonName.DPadDown, GamepadButton.DPadDown)]
    [InlineData(ButtonName.DPadLeft, GamepadButton.DPadLeft)]
    public void TryGetMappedButton_KnownButtonName_ShouldMapCorrectly(
        ButtonName silkButton,
        GamepadButton expected
    )
    {
        var found = GamepadButtonMapper.TryGetMappedButton(silkButton, out var mapped);

        Assert.True(found);
        Assert.Equal(expected, mapped);
    }

    [Fact]
    public void TryGetMappedButton_Unknown_ShouldReturnFalse()
    {
        var found = GamepadButtonMapper.TryGetMappedButton(ButtonName.Unknown, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGetMappedButton_EveryDeclaredButtonNameExceptUnknown_ShouldBeMapped()
    {
        // Guards against silently forgetting a new Silk.NET ButtonName if the package is
        // upgraded and adds one — every value should be either mapped here or Unknown.
        foreach (var value in Enum.GetValues<ButtonName>())
        {
            if (value == ButtonName.Unknown)
                continue;

            Assert.True(
                GamepadButtonMapper.TryGetMappedButton(value, out _),
                $"{value} has no GamepadButton mapping."
            );
        }
    }
}
