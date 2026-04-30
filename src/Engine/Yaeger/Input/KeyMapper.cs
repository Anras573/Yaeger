using Silk.NET.Input;

namespace Yaeger.Input;

public static class KeyMapper
{
    private static readonly Dictionary<Key, Keys> KeyMap = new()
    {
        { Key.W, Keys.W },
        { Key.A, Keys.A },
        { Key.S, Keys.S },
        { Key.D, Keys.D },
        { Key.Q, Keys.Q },
        { Key.E, Keys.E },
        { Key.Space, Keys.Space },
        { Key.Escape, Keys.Escape },
        { Key.Up, Keys.Up },
        { Key.Down, Keys.Down },
        { Key.Left, Keys.Left },
        { Key.Right, Keys.Right },
        { Key.R, Keys.R },
        { Key.I, Keys.I },
        { Key.J, Keys.J },
        { Key.H, Keys.H },
        { Key.Number1, Keys.Num1 },
        { Key.Number2, Keys.Num2 },
        { Key.Number3, Keys.Num3 },
        { Key.C, Keys.C },
        // Add more mappings as needed
    };

    public static bool TryGetMappedKey(Key key, out Keys mappedKey) =>
        KeyMap.TryGetValue(key, out mappedKey);
}
