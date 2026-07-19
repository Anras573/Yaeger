namespace Yaeger.Input;

/// <summary>
/// Curated gamepad button set, mirroring <see cref="Keys"/>'s approach for keyboard keys.
/// <see cref="LeftTrigger"/>/<see cref="RightTrigger"/> are synthesized by <c>Gamepad</c> from
/// the analog trigger values crossing a threshold — the underlying Silk.NET button list has no
/// trigger entries of its own, since triggers are normally analog-only.
/// </summary>
public enum GamepadButton
{
    A,
    B,
    X,
    Y,
    LeftBumper,
    RightBumper,
    LeftTrigger,
    RightTrigger,
    Back,
    Start,
    Home,
    LeftStickButton,
    RightStickButton,
    DPadUp,
    DPadRight,
    DPadDown,
    DPadLeft,
    // Add more as needed
}
