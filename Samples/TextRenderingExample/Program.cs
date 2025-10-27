using System.Numerics;

using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace TextRenderingExample;

public class Program
{
    private static Window? _window;
    private static World? _world;
    private static TextRenderer? _textRenderer;
    private static TextRenderSystem? _textRenderSystem;
    private static FontManager? _fontManager;

    public static void Main()
    {
        _window = Window.Create();

        _window.OnLoad += OnLoad;
        _window.OnUpdate += OnUpdate;
        _window.OnRender += OnRender;
        _window.OnClosing += OnClosing;

        Keyboard.AddKeyDown(Keys.Escape, () => _window.Close());

        _window.Run();

        Console.WriteLine("Window closed");
    }

    private static void OnLoad()
    {
        Console.WriteLine("Text Rendering Example - Loading...");

        if (_window == null)
            return;

        // Initialize ECS World
        _world = new World();

        // Initialize Font System
        _fontManager = new FontManager();

        // Try to load a system font (this is a placeholder - in a real scenario you'd provide a font file)
        // For demonstration purposes, we'll create a mock font path
        // In a real application, you would provide a valid .ttf file path
        Console.WriteLine("Note: Text rendering requires a valid TrueType font file.");
        Console.WriteLine("Please provide a .ttf font file in the Assets directory to see rendered text.");

        // Initialize Text Renderer
        _textRenderer = new TextRenderer(_window);

        // Create Text Render System
        _textRenderSystem = new TextRenderSystem(_textRenderer, _world);

        // Note: In a real application, you would load a font like this:
        var defaultFont = _fontManager.Load("Assets/Roboto-Regular.ttf");
        //
        // And create text entities like this:
        var textEntity = _world.CreateEntity();
        _world.AddComponent(textEntity, new Text("Hello, Yaeger!", defaultFont, 12, Color.White));
        _world.AddComponent(textEntity, new Transform2D
        {
            Position = new Vector2(-0.5f, 0.0f),
            Scale = new Vector2(0.01f, 0.01f) // Scale down for screen-space rendering
        });

        Console.WriteLine("Text Rendering Example - Loaded successfully!");
        Console.WriteLine("To see text rendering in action:");
        Console.WriteLine("1. Add a .ttf font file to an Assets/fonts directory");
        Console.WriteLine("2. Uncomment the font loading code in Program.cs");
        Console.WriteLine("3. Rebuild and run the example");
    }

    private static void OnUpdate(double deltaTime)
    {
        // Update logic here if needed
    }

    private static void OnRender(double deltaTime)
    {
        // Render all text entities
        _textRenderSystem?.Render();
    }

    private static void OnClosing()
    {
        Console.WriteLine("Text Rendering Example - Closing...");

        _textRenderer?.Dispose();
        _fontManager?.Dispose();
        _window?.Dispose();

        Console.WriteLine("Text Rendering Example - Closed successfully!");
    }
}