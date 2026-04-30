namespace Yaeger.Input;

public static class MouseButtonMapper
{
    private static readonly Dictionary<Silk.NET.Input.MouseButton, MouseButton> ButtonMap = new()
    {
        { Silk.NET.Input.MouseButton.Left, MouseButton.Left },
        { Silk.NET.Input.MouseButton.Right, MouseButton.Right },
        { Silk.NET.Input.MouseButton.Middle, MouseButton.Middle },
        { Silk.NET.Input.MouseButton.Button4, MouseButton.Side1 },
        { Silk.NET.Input.MouseButton.Button5, MouseButton.Side2 },
        // Add more mappings as needed
    };

    public static bool TryGetMappedButton(
        Silk.NET.Input.MouseButton button,
        out MouseButton mappedButton
    ) => ButtonMap.TryGetValue(button, out mappedButton);
}
