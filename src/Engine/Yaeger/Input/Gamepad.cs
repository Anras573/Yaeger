using System.Numerics;
using Silk.NET.Input;

namespace Yaeger.Input;

/// <summary>
/// Static gamepad input surface. Mirrors <see cref="Keyboard"/>/<see cref="Mouse"/> — initialised
/// once by the window, queried from anywhere. Exposes button state (polling + events), analog
/// sticks (raw and deadzone-filtered), and analog trigger values, plus connect/disconnect events.
/// </summary>
/// <remarks>
/// Native-only for now: there is no browser-backend implementation (the platform seam for input
/// is <c>Yaeger.Platform.IInputState</c>, in <c>Yaeger.Core</c>, which does not yet expose
/// gamepad state — see <c>docs</c> for the current native/browser split).
/// </remarks>
public static class Gamepad
{
    // Threshold at which an analog trigger position is considered "pressed" for the purposes of
    // GamepadButton.LeftTrigger/RightTrigger — Silk.NET's button list has no trigger entries of
    // its own, since triggers are normally analog-only.
    private const float TriggerButtonThreshold = 0.5f;

    private static IInputContext? _inputContext;
    private static IGamepad? _gamepad;
    private static readonly HashSet<GamepadButton> PressedButtons = [];

    private static readonly Dictionary<GamepadButton, Action> ButtonDownActions = new();
    private static readonly Dictionary<GamepadButton, Action> ButtonUpActions = new();
    private static readonly List<Action> ConnectedActions = [];
    private static readonly List<Action> DisconnectedActions = [];

    private static Vector2 _leftStickRaw;
    private static Vector2 _rightStickRaw;
    private static float _leftTriggerRaw;
    private static float _rightTriggerRaw;

    /// <summary>
    /// Radial deadzone applied to <see cref="LeftStick"/>/<see cref="RightStick"/> (not to the
    /// <c>*Raw</c> variants). A stick magnitude at or below this value reads as
    /// <see cref="Vector2.Zero"/>; above it, the output ramps back up to full magnitude at the
    /// stick's physical limit, avoiding a jump straight from zero to
    /// <c>magnitude - Deadzone</c> right at the edge. Defaults to 0.15.
    /// </summary>
    public static float Deadzone { get; set; } = 0.15f;

    /// <summary>Whether a gamepad is currently attached (the first one found, if several are connected).</summary>
    public static bool IsConnected => _gamepad is not null;

    /// <summary>Left stick position, unfiltered. Each axis is in [-1, 1].</summary>
    public static Vector2 LeftStickRaw => _leftStickRaw;

    /// <summary>Right stick position, unfiltered. Each axis is in [-1, 1].</summary>
    public static Vector2 RightStickRaw => _rightStickRaw;

    /// <summary>Left stick position with <see cref="Deadzone"/> applied.</summary>
    public static Vector2 LeftStick => ApplyDeadzone(_leftStickRaw, Deadzone);

    /// <summary>Right stick position with <see cref="Deadzone"/> applied.</summary>
    public static Vector2 RightStick => ApplyDeadzone(_rightStickRaw, Deadzone);

    /// <summary>Left trigger analog value, in [0, 1].</summary>
    public static float LeftTriggerValue => _leftTriggerRaw;

    /// <summary>Right trigger analog value, in [0, 1].</summary>
    public static float RightTriggerValue => _rightTriggerRaw;

    internal static void Initialize(IInputContext inputContext)
    {
        if (_inputContext != null)
            return;

        _inputContext = inputContext;
        _inputContext.ConnectionChanged += OnConnectionChanged;

        var pad = inputContext.Gamepads.FirstOrDefault(g => g.IsConnected);
        if (pad is not null)
            Attach(pad);
    }

    /// <summary>
    /// Binds an action to be executed when the specified button is pressed down.
    /// </summary>
    /// <warning>This will overwrite any existing action for the specified button.</warning>
    public static void AddButtonDown(GamepadButton button, Action action) =>
        ButtonDownActions[button] = action;

    /// <summary>
    /// Binds an action to be executed when the specified button is released.
    /// </summary>
    /// <warning>This will overwrite any existing action for the specified button.</warning>
    public static void AddButtonUp(GamepadButton button, Action action) =>
        ButtonUpActions[button] = action;

    /// <summary>Adds an action invoked whenever a gamepad becomes the active one.</summary>
    public static void AddConnected(Action action) => ConnectedActions.Add(action);

    /// <summary>Adds an action invoked whenever the active gamepad disconnects.</summary>
    public static void AddDisconnected(Action action) => DisconnectedActions.Add(action);

    public static bool IsButtonPressed(GamepadButton button) => PressedButtons.Contains(button);

    /// <summary>
    /// Radial deadzone: magnitudes at or below <paramref name="deadzone"/> map to zero; above it,
    /// output rescales linearly from 0 (at the deadzone edge) to 1 (at magnitude 1), so the stick
    /// doesn't jump straight from nothing to <c>magnitude - deadzone</c> the instant it clears
    /// the deadzone.
    /// </summary>
    internal static Vector2 ApplyDeadzone(Vector2 raw, float deadzone)
    {
        var magnitude = raw.Length();
        if (magnitude <= deadzone || deadzone >= 1f)
            return Vector2.Zero;

        var direction = raw / magnitude;
        var scaled = Math.Clamp((magnitude - deadzone) / (1f - deadzone), 0f, 1f);
        return direction * scaled;
    }

    private static void Attach(IGamepad pad)
    {
        _gamepad = pad;
        pad.ButtonDown += OnButtonDown;
        pad.ButtonUp += OnButtonUp;
        pad.ThumbstickMoved += OnThumbstickMoved;
        pad.TriggerMoved += OnTriggerMoved;

        foreach (var action in ConnectedActions)
            action();
    }

    private static void Detach()
    {
        if (_gamepad is null)
            return;

        _gamepad.ButtonDown -= OnButtonDown;
        _gamepad.ButtonUp -= OnButtonUp;
        _gamepad.ThumbstickMoved -= OnThumbstickMoved;
        _gamepad.TriggerMoved -= OnTriggerMoved;
        _gamepad = null;

        PressedButtons.Clear();
        _leftStickRaw = Vector2.Zero;
        _rightStickRaw = Vector2.Zero;
        _leftTriggerRaw = 0f;
        _rightTriggerRaw = 0f;

        foreach (var action in DisconnectedActions)
            action();
    }

    private static void OnConnectionChanged(IInputDevice device, bool connected)
    {
        if (device is not IGamepad pad)
            return;

        if (connected)
        {
            // First-pad-wins, matching Keyboard/Mouse: only attach if nothing is active yet.
            if (_gamepad is null)
                Attach(pad);
        }
        else if (ReferenceEquals(pad, _gamepad))
        {
            Detach();

            // Promote another already-connected pad, if any, so "first pad wins" still holds
            // after the active one disconnects.
            var next = _inputContext?.Gamepads.FirstOrDefault(g => g.IsConnected);
            if (next is not null)
                Attach(next);
        }
    }

    private static void OnButtonDown(IGamepad _, Button button)
    {
        if (!GamepadButtonMapper.TryGetMappedButton(button.Name, out var mapped))
            return;

        if (PressedButtons.Add(mapped))
        {
            ButtonDownActions.TryGetValue(mapped, out var action);
            action?.Invoke();
        }
    }

    private static void OnButtonUp(IGamepad _, Button button)
    {
        if (!GamepadButtonMapper.TryGetMappedButton(button.Name, out var mapped))
            return;

        if (PressedButtons.Remove(mapped))
        {
            ButtonUpActions.TryGetValue(mapped, out var action);
            action?.Invoke();
        }
    }

    private static void OnThumbstickMoved(IGamepad _, Thumbstick stick)
    {
        // Index 0 = left stick, index 1 = right stick — the standard layout Silk.NET reports
        // for Xbox-style controllers.
        switch (stick.Index)
        {
            case 0:
                _leftStickRaw = new Vector2(stick.X, stick.Y);
                break;
            case 1:
                _rightStickRaw = new Vector2(stick.X, stick.Y);
                break;
        }
    }

    private static void OnTriggerMoved(IGamepad _, Trigger trigger)
    {
        // Index 0 = left trigger, index 1 = right trigger — same layout convention as above.
        switch (trigger.Index)
        {
            case 0:
                _leftTriggerRaw = trigger.Position;
                UpdateTriggerButton(GamepadButton.LeftTrigger, trigger.Position);
                break;
            case 1:
                _rightTriggerRaw = trigger.Position;
                UpdateTriggerButton(GamepadButton.RightTrigger, trigger.Position);
                break;
        }
    }

    private static void UpdateTriggerButton(GamepadButton button, float value)
    {
        var isPressed = value >= TriggerButtonThreshold;
        var wasPressed = PressedButtons.Contains(button);

        if (isPressed && !wasPressed)
        {
            PressedButtons.Add(button);
            ButtonDownActions.TryGetValue(button, out var action);
            action?.Invoke();
        }
        else if (!isPressed && wasPressed)
        {
            PressedButtons.Remove(button);
            ButtonUpActions.TryGetValue(button, out var action);
            action?.Invoke();
        }
    }
}
