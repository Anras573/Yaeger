using Silk.NET.Input;

namespace Yaeger.Input;

/// <summary>
/// Maps Silk.NET's <see cref="ButtonName"/> to the curated <see cref="GamepadButton"/> set,
/// mirroring <see cref="KeyMapper"/>. <see cref="GamepadButton.LeftTrigger"/>/
/// <see cref="GamepadButton.RightTrigger"/> have no <see cref="ButtonName"/> counterpart (they're
/// synthesized from analog trigger values elsewhere) and so are absent from this map, as is the
/// <see cref="ButtonName.Unknown"/> sentinel.
/// </summary>
public static class GamepadButtonMapper
{
    private static readonly Dictionary<ButtonName, GamepadButton> ButtonMap = new()
    {
        { ButtonName.A, GamepadButton.A },
        { ButtonName.B, GamepadButton.B },
        { ButtonName.X, GamepadButton.X },
        { ButtonName.Y, GamepadButton.Y },
        { ButtonName.LeftBumper, GamepadButton.LeftBumper },
        { ButtonName.RightBumper, GamepadButton.RightBumper },
        { ButtonName.Back, GamepadButton.Back },
        { ButtonName.Start, GamepadButton.Start },
        { ButtonName.Home, GamepadButton.Home },
        { ButtonName.LeftStick, GamepadButton.LeftStickButton },
        { ButtonName.RightStick, GamepadButton.RightStickButton },
        { ButtonName.DPadUp, GamepadButton.DPadUp },
        { ButtonName.DPadRight, GamepadButton.DPadRight },
        { ButtonName.DPadDown, GamepadButton.DPadDown },
        { ButtonName.DPadLeft, GamepadButton.DPadLeft },
        // Add more mappings as needed
    };

    public static bool TryGetMappedButton(ButtonName name, out GamepadButton mappedButton) =>
        ButtonMap.TryGetValue(name, out mappedButton);
}
