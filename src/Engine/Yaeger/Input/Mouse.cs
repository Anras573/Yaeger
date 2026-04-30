using System.Numerics;
using Silk.NET.Input;
using SilkMouseButton = Silk.NET.Input.MouseButton;

namespace Yaeger.Input;

/// <summary>
/// Static mouse input surface. Mirrors <see cref="Keyboard"/> — initialised once by the
/// window, queried from anywhere. Exposes button state (polling + events), cursor position
/// in both client pixels and NDC, and scroll wheel input.
/// </summary>
/// <remarks>
/// <see cref="Position"/> is in client pixels with origin at the top-left.
/// <see cref="PositionNdc"/> is in OpenGL normalised device coordinates
/// (<c>-1..1</c> on both axes, origin at the centre, Y up). World-space conversion
/// is a separate concern handled by the caller using an inverse <c>Camera2D.ViewProjection</c>.
/// </remarks>
public static class Mouse
{
    private static IMouse? _mouse;
    private static readonly HashSet<MouseButton> PressedButtons = [];

    private static readonly Dictionary<MouseButton, Action> ButtonDownActions = new();
    private static readonly Dictionary<MouseButton, Action> ButtonUpActions = new();
    private static readonly List<Action<float>> ScrollActions = [];

    private static Vector2 _position;
    private static Vector2 _previousPosition;
    private static float _scrollAccumulator;
    private static Vector2 _windowSize = new(1f, 1f);

    /// <summary>Current cursor position in client pixels (origin: top-left).</summary>
    public static Vector2 Position => _position;

    /// <summary>Movement in pixels since the previous frame.</summary>
    public static Vector2 PositionDelta => _position - _previousPosition;

    /// <summary>
    /// Current cursor position in OpenGL NDC (<c>-1..1</c>, origin centre, Y up). Requires the
    /// window to have reported a size; returns <see cref="Vector2.Zero"/> before the first frame.
    /// </summary>
    public static Vector2 PositionNdc
    {
        get
        {
            if (_windowSize.X <= 0 || _windowSize.Y <= 0)
                return Vector2.Zero;
            // Silk.NET position is top-left-origin Y-down; flip to NDC bottom-origin Y-up.
            var x = (_position.X / _windowSize.X) * 2f - 1f;
            var y = 1f - (_position.Y / _windowSize.Y) * 2f;
            return new Vector2(x, y);
        }
    }

    /// <summary>Accumulated vertical scroll wheel delta since the last frame boundary.</summary>
    public static float ScrollDelta => _scrollAccumulator;

    internal static void Initialize(IInputContext inputContext)
    {
        if (_mouse != null)
            return;
        _mouse = inputContext.Mice.Count > 0 ? inputContext.Mice[0] : null;
        if (_mouse == null)
            return;
        _mouse.MouseDown += OnButtonDown;
        _mouse.MouseUp += OnButtonUp;
        _mouse.MouseMove += OnMove;
        _mouse.Scroll += OnScroll;
    }

    /// <summary>Must be called by the window whenever its size changes (used for NDC math).</summary>
    internal static void SetWindowSize(Vector2 size)
    {
        _windowSize = size;
    }

    /// <summary>Call once per frame after input consumers have read the delta values.</summary>
    internal static void EndFrame()
    {
        _previousPosition = _position;
        _scrollAccumulator = 0f;
    }

    public static void AddButtonDown(MouseButton button, Action action)
    {
        ButtonDownActions[button] = action;
    }

    public static void AddButtonUp(MouseButton button, Action action)
    {
        ButtonUpActions[button] = action;
    }

    public static void AddScroll(Action<float> action)
    {
        ScrollActions.Add(action);
    }

    public static bool IsButtonPressed(MouseButton button) => PressedButtons.Contains(button);

    private static void OnButtonDown(IMouse _, SilkMouseButton button)
    {
        if (!MouseButtonMapper.TryGetMappedButton(button, out var mapped))
            return;

        if (PressedButtons.Add(mapped))
        {
            ButtonDownActions.TryGetValue(mapped, out var action);
            action?.Invoke();
        }
    }

    private static void OnButtonUp(IMouse _, SilkMouseButton button)
    {
        if (!MouseButtonMapper.TryGetMappedButton(button, out var mapped))
            return;

        if (PressedButtons.Remove(mapped))
        {
            ButtonUpActions.TryGetValue(mapped, out var action);
            action?.Invoke();
        }
    }

    private static void OnMove(IMouse _, System.Numerics.Vector2 position)
    {
        _position = position;
    }

    private static void OnScroll(IMouse _, ScrollWheel wheel)
    {
        _scrollAccumulator += wheel.Y;
        foreach (var action in ScrollActions)
        {
            action(wheel.Y);
        }
    }
}
