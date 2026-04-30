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

    // The mix of sizes below is deliberate and doubles as a visual regression test:
    //   - Multiple sizes of one font coexist — proves the atlas is keyed by (font, size)
    //     rather than font alone (fix for a bug where the first size rendered became
    //     permanent for the rest of the session).
    //   - 22, 18, 14, 10 are NOT multiples of 4 — proves the glyph upload path respects
    //     GL_UNPACK_ALIGNMENT = 1 (fix for a bug where sub-multiple-of-4 sizes produced
    //     sheared/striped glyphs). If any of these lines render as stripes rather than
    //     readable text, the alignment fix has regressed.

    AddTextOverlay(defaultFont, "Hello, Yaeger!", new Vector2(-0.95f, 0.60f), 32, Color.White);
    AddTextOverlay(
        defaultFont,
        "The quick brown fox jumps over the lazy dog",
        new Vector2(-0.95f, 0.35f),
        22,
        Color.Green
    );
    AddTextOverlay(
        defaultFont,
        "fontSize 18 renders cleanly (not a multiple of 4)",
        new Vector2(-0.95f, 0.15f),
        18,
        Color.White
    );
    AddTextOverlay(defaultFont, "and 14 too", new Vector2(-0.95f, 0.00f), 14, Color.White);
    AddTextOverlay(defaultFont, "even 10 pt", new Vector2(-0.95f, -0.12f), 10, Color.White);
    AddTextOverlay(defaultFont, "Press ESC to exit", new Vector2(-0.95f, -0.30f), 16, Color.Blue);
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
