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

Keyboard.AddKeyDown(Keys.Escape, () => window.Close());

window.Run();

Console.WriteLine("Window closed");

return;

void OnLoad()
{
    Console.WriteLine("Text Rendering Example - Loading...");

    var defaultFont = fontManager.Load("Assets/Roboto-Regular.ttf");

    var textEntity = world.CreateEntity();
    world.AddComponent(textEntity, new Text("Hello, Yaeger!", defaultFont, 48, Color.White));
    world.AddComponent(textEntity, new Transform2D
    {
        Position = new Vector2(-0.95f, 0),
        Scale = new Vector2(0.005f, 0.005f) // Scale down for screen-space rendering
    });

    var textEntity2 = world.CreateEntity();
    world.AddComponent(textEntity2, new Text("The quick brown fox jumps over the lazy dog", defaultFont, 32, Color.Green));
    world.AddComponent(textEntity2, new Transform2D
    {
        Position = new Vector2(-0.95f, -0.2f),
        Scale = new Vector2(0.003f, 0.003f) // Scale down for screen-space rendering
    });

    var textEntity3 = world.CreateEntity();
    world.AddComponent(textEntity3, new Text("Press ESC to exit", defaultFont, 24, Color.Blue));
    world.AddComponent(textEntity3, new Transform2D
    {
        Position = new Vector2(-0.95f, -0.4f),
        Scale = new Vector2(0.002f, 0.002f) // Scale down for screen-space rendering
    });
}

void OnRender(double deltaTime)
{
    // Render all text entities
    textRenderSystem.Render();
}

void OnClosing()
{
    Console.WriteLine("Text Rendering Example - Closing...");

    textRenderer.Dispose();
    fontManager.Dispose();

    Console.WriteLine("Text Rendering Example - Closed successfully!");
}