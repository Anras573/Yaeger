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
var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);
var animationSystem = new AnimationSystem(world);

Keyboard.AddKeyDown(Keys.Escape, window.Close);

var defaultFont = fontManager.Load("Assets/Roboto-Regular.ttf");
var texts = new[]
{
    "Press 'W' to walk",
    "Press 'R' to run",
    "Press 'J' to jump",
    "Press 'I' for idle",
    "Press '1', '2', or '3' for attacks",
    "Press 'H' for hurt",
    "Press 'S' for shield",
    "Press 'D' for dead",
};

for (int i = 0; i < texts.Length; i++)
    AddTextOverlay(world, defaultFont, texts[i], new Vector2(-0.95f, 0.9f - i * 0.1f));

static void AddTextOverlay(World world, Font font, string content, Vector2 position)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, new Text(content, font, 24, Color.White));
    world.AddComponent(
        entity,
        new Transform2D
        {
            Position = position,
            Scale = new Vector2(0.003f, 0.003f), // Scale down for screen-space rendering
        }
    );
}

// ------------------------------------------------------------------
// Helper: build an Animation where every frame has the same duration.
// ------------------------------------------------------------------
static Animation MakeAnimation(int frameCount, float frameDuration, bool loop = true)
{
    var frames = new AnimationFrame[frameCount];
    for (int i = 0; i < frameCount; i++)
        frames[i] = new AnimationFrame($"_placeholder_{i}", frameDuration);
    return new Animation(frames, loop);
}

// ------------------------------------------------------------------
// Sprite-sheet definitions.
// Each row of the sheet is a single-row horizontal strip.
// ------------------------------------------------------------------
var sheets = new Dictionary<string, SpriteSheet>
{
    ["Idle"] = new SpriteSheet("Assets/Idle.png", columns: 6),
    ["Walk"] = new SpriteSheet("Assets/Walk.png", columns: 8),
    ["Run"] = new SpriteSheet("Assets/Run.png", columns: 8),
    ["Jump"] = new SpriteSheet("Assets/Jump.png", columns: 12),
    ["Attack_1"] = new SpriteSheet("Assets/Attack_1.png", columns: 6),
    ["Attack_2"] = new SpriteSheet("Assets/Attack_2.png", columns: 4),
    ["Attack_3"] = new SpriteSheet("Assets/Attack_3.png", columns: 3),
    ["Hurt"] = new SpriteSheet("Assets/Hurt.png", columns: 2),
    ["Shield"] = new SpriteSheet("Assets/Shield.png", columns: 2),
    ["Dead"] = new SpriteSheet("Assets/Dead.png", columns: 3),
};

// ------------------------------------------------------------------
// Create the samurai entity with the Idle animation to start.
// ------------------------------------------------------------------
var samurai = world.CreateEntity("samurai");
var currentSheetName = "Idle";

void ApplyAnimation(string name)
{
    var sheet = sheets[name];
    var anim = MakeAnimation(sheet.FrameCount, frameDuration: 0.1f, loop: name != "Dead");
    world.AddComponent(samurai, sheet);
    world.AddComponent(samurai, anim);
    world.AddComponent(samurai, new AnimationState(0, 0f, false));
    currentSheetName = name;
}

ApplyAnimation("Idle");
world.AddComponent(samurai, new Transform2D(Vector2.Zero, 0f, Vector2.One));

// ------------------------------------------------------------------
// Key bindings: switch animations at runtime.
// ------------------------------------------------------------------
Keyboard.AddKeyDown(Keys.W, () => ApplyAnimation("Walk"));
Keyboard.AddKeyDown(Keys.R, () => ApplyAnimation("Run"));
Keyboard.AddKeyDown(Keys.J, () => ApplyAnimation("Jump"));
Keyboard.AddKeyDown(Keys.I, () => ApplyAnimation("Idle"));
Keyboard.AddKeyDown(Keys.Num1, () => ApplyAnimation("Attack_1"));
Keyboard.AddKeyDown(Keys.Num2, () => ApplyAnimation("Attack_2"));
Keyboard.AddKeyDown(Keys.Num3, () => ApplyAnimation("Attack_3"));
Keyboard.AddKeyDown(Keys.H, () => ApplyAnimation("Hurt"));
Keyboard.AddKeyDown(Keys.S, () => ApplyAnimation("Shield"));
Keyboard.AddKeyDown(Keys.D, () => ApplyAnimation("Dead"));

window.OnUpdate += deltaTime => animationSystem.Update((float)deltaTime);
window.OnRender += _ => renderSystem.Render();
window.OnRender += _ => textRenderSystem.Render();

window.Run();
