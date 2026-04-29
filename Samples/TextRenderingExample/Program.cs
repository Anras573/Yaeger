using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

using var window = Window.Create();
var world = new World();
var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);

window.OnLoad += OnLoad;
window.OnRender += OnRender;
window.OnClosing += OnClosing;

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.Run();

Console.WriteLine("Window closed");

return;

void AddTextOverlay(Font font, string content, Vector2 position, int fontSize, Color color)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, new Text(content, font, fontSize, color));
    world.AddComponent(
        entity,
        new Transform2D { Position = position, Scale = new Vector2(0.003f, 0.003f) }
    );
}

void OnLoad()
{
    Console.WriteLine("Text Rendering Example - Loading...");

    var defaultFont = fontManager.Load("Assets/Roboto-Regular.ttf");

    AddTextOverlay(defaultFont, "Hello, Yaeger!", new Vector2(-0.95f, 0), 24, Color.White);
    AddTextOverlay(
        defaultFont,
        "The quick brown fox jumps over the lazy dog",
        new Vector2(-0.95f, -0.2f),
        16,
        Color.Green
    );
    AddTextOverlay(defaultFont, "Press ESC to exit", new Vector2(-0.95f, -0.4f), 8, Color.Blue);
}

void OnRender(double deltaTime)
{
    textRenderSystem.Render();
}

void OnClosing()
{
    Console.WriteLine("Text Rendering Example - Closing...");

    textRenderer.Dispose();
    fontManager.Dispose();

    Console.WriteLine("Text Rendering Example - Closed successfully!");
}
