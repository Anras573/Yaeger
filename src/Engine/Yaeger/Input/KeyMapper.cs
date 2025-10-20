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
        { Key.Space, Keys.Space },
        { Key.Escape, Keys.Escape },
        { Key.Up, Keys.Up },
        { Key.Down, Keys.Down }
        // Add more mappings as needed
    };

    public static bool TryGetMappedKey(Key key, out Keys mappedKey) =>
        KeyMap.TryGetValue(key, out mappedKey);
}