using Silk.NET.Input;

namespace Yaeger.Input;

public static class Keyboard
{
    private static IKeyboard? _keyboard;
    private static readonly HashSet<Keys> PressedKeys = [];
    private static readonly Dictionary<Key, Keys> KeyMap = new()
    {
        { Key.W, Keys.W },
        { Key.A, Keys.A },
        { Key.S, Keys.S },
        { Key.D, Keys.D },
        { Key.Space, Keys.Space },
        { Key.Escape, Keys.Escape },
        { Key.Up, Keys.Up },
        { Key.Down, Keys.Down }
        // Add more mappings as needed
    };

    internal static void Initialize(IInputContext inputContext)
    {
        if (_keyboard != null) return;
        _keyboard = inputContext.Keyboards.Count > 0 ? inputContext.Keyboards[0] : null;
        if (_keyboard == null) return;
        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
    }

    private static void OnKeyDown(IKeyboard _, Key key, int _2)
    {
        if (KeyMap.TryGetValue(key, out var mappedKey))
            PressedKeys.Add(mappedKey);
    }

    private static void OnKeyUp(IKeyboard _, Key key, int _2)
    {
        if (KeyMap.TryGetValue(key, out var mappedKey))
            PressedKeys.Remove(mappedKey);
    }

    public static bool IsKeyPressed(Keys key) => PressedKeys.Contains(key);
}

