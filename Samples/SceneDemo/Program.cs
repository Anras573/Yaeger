using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Scene demo: load a multi-entity world from a JSON scene file, then exercise runtime
// access via the tags the scene file declared.
//
// The scene at Scenes/level1.json contains 7 entities:
//   - "ground", "player", "sun" — tagged, looked up after load to prove tags round-trip
//   - 4 anonymous "stars" — prove the tagless path works in the same file
//
// After loading we animate the "player" entity by reading it back by tag, showing that
// scene-loaded entities compose naturally with runtime systems.
//
// Controls:
//   ESC  exit

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world, window);

var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);

var registry = new ComponentRegistry().RegisterEngineComponents();
var sceneLoader = new SceneLoader(registry);
var scene = sceneLoader.Load("Scenes/level1.json");

var created = world.Instantiate(scene);
Console.WriteLine($"Loaded scene with {created.Count} entities.");

// Tag round-trip: the scene declared "player" — fetch it back and remember its origin.
var player = world.GetEntity("player");
var playerHome = world.GetComponent<Transform2D>(player).Position;

// HUD text — screen-space, untouched by runtime animation of the world entities.
var font = fontManager.Load("Assets/Roboto-Regular.ttf");
var hudEntity = world.CreateEntity("hud");

// HUD text is ASCII-only — Font.Shape has a pre-existing bug with multi-byte UTF-8
// characters (see issue #38). An em dash or any non-ASCII glyph crashes with
// IndexOutOfRangeException in Font.Shape. Avoid until the font pipeline is fixed.
world.AddComponent(
    hudEntity,
    new Text(
        $"Scene: Scenes/level1.json, {created.Count} entities loaded (tags: ground, player, sun)",
        font,
        14,
        Color.White
    )
);
world.AddComponent(
    hudEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.92f), Scale = new Vector2(0.003f) }
);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

var elapsed = 0.0;
window.OnUpdate += dt =>
{
    // Orbit the "player" sprite around its scene-declared position to prove that
    // tag-based lookup gives us live access to a scene-loaded entity.
    elapsed += dt;
    var transform = world.GetComponent<Transform2D>(player);
    var radius = 0.25f;
    var angle = (float)elapsed * 1.5f;
    transform.Position = new Vector2(
        playerHome.X + MathF.Cos(angle) * radius,
        playerHome.Y + MathF.Sin(angle) * radius * 0.4f
    );
    world.AddComponent(player, transform);
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
