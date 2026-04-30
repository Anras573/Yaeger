using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Mouse demo: paint a trail of sprites while the left button is held, right-click to clear.
// Demonstrates polling (IsButtonPressed), events (AddButtonDown), NDC position derivation
// from pixel input, and scroll-wheel accumulation.
//
// Controls:
//   LMB (hold)      paint sprites at mouse position
//   RMB (click)     clear the canvas
//   Scroll wheel    changes the sprite size (accumulator shown in HUD)
//   ESC             exit

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world, window);

var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);

const string spriteTexture = "Assets/square.png";
var font = fontManager.Load("Assets/Roboto-Regular.ttf");

var paintedEntities = new List<Entity>();
var spriteScale = 0.03f;
const float minScale = 0.01f;
const float maxScale = 0.15f;
const float scrollSensitivity = 0.005f;

var hudEntity = world.CreateEntity("hud");
world.AddComponent(hudEntity, new Text("", font, 18, Color.White));
world.AddComponent(
    hudEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.9f), Scale = new Vector2(0.003f) }
);

var instructionsEntity = world.CreateEntity("hud-instructions");
world.AddComponent(
    instructionsEntity,
    new Text("LMB paint   RMB clear   Scroll resize   ESC exit", font, 14, Color.White)
);
world.AddComponent(
    instructionsEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.82f), Scale = new Vector2(0.003f) }
);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

Mouse.AddButtonDown(
    MouseButton.Right,
    () =>
    {
        foreach (var entity in paintedEntities)
            world.DestroyEntity(entity);
        paintedEntities.Clear();
    }
);

Mouse.AddScroll(delta =>
{
    spriteScale = Math.Clamp(spriteScale + delta * scrollSensitivity, minScale, maxScale);
});

window.OnUpdate += _ =>
{
    if (Mouse.IsButtonPressed(MouseButton.Left))
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite(spriteTexture));
        world.AddComponent(
            entity,
            new Transform2D(Mouse.PositionNdc, 0f, new Vector2(spriteScale))
        );
        paintedEntities.Add(entity);
    }

    var hud = new Text(
        $"px ({Mouse.Position.X:F0}, {Mouse.Position.Y:F0})   ndc ({Mouse.PositionNdc.X:F2}, {Mouse.PositionNdc.Y:F2})   size {spriteScale:F3}   painted {paintedEntities.Count}",
        font,
        18,
        Color.White
    );
    world.AddComponent(hudEntity, hud);
};

window.OnRender += _ =>
{
    renderSystem.Render();
    textRenderSystem.Render();
};

window.OnClosing += () =>
{
    textRenderer.Dispose();
    fontManager.Dispose();
    renderer.Dispose();
};

window.Run();
