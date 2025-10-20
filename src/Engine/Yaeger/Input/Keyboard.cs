using Silk.NET.Input;

namespace Yaeger.Input;

public static class Keyboard
{
    private static IKeyboard? _keyboard;
    private static readonly HashSet<Keys> PressedKeys = [];

    private static readonly Dictionary<Keys, Action> KeyDownActions = new();
    private static readonly Dictionary<Keys, Action> KeyUpActions = new();

    internal static void Initialize(IInputContext inputContext)
    {
        if (_keyboard != null) return;
        _keyboard = inputContext.Keyboards.Count > 0 ? inputContext.Keyboards[0] : null;
        if (_keyboard == null) return;
        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
    }

    /// <summary>
    /// Binds an action to be executed when the specified key is pressed down.
    /// </summary>
    /// <warning>This will overwrite any existing action for the specified key.</warning>
    /// <param name="key"></param>
    /// <param name="action"></param>
    public static void AddKeyDown(Keys key, Action action)
    {
        KeyDownActions[key] = action;
    }

    /// <summary>
    /// Binds an action to be executed when the specified key is released.
    /// </summary>
    /// <warning>This will overwrite any existing action for the specified key.</warning>
    /// <param name="key"></param>
    /// <param name="action"></param>
    public static void AddKeyUp(Keys key, Action action)
    {
        KeyUpActions[key] = action;
    }

    private static void OnKeyDown(IKeyboard _, Key key, int _2)
    {
        if (!KeyMapper.TryGetMappedKey(key, out var mappedKey))
            return;

        if (PressedKeys.Add(mappedKey))
        {
            KeyDownActions.TryGetValue(mappedKey, out var action);
            action?.Invoke();
        }
    }

    private static void OnKeyUp(IKeyboard _, Key key, int _2)
    {
        if (!KeyMapper.TryGetMappedKey(key, out var mappedKey))
            return;

        if (PressedKeys.Remove(mappedKey))
        {
            KeyUpActions.TryGetValue(mappedKey, out var action);
            action?.Invoke();
        }
    }

    public static bool IsKeyPressed(Keys key) => PressedKeys.Contains(key);
}