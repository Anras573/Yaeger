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
    "Press 'C' for combat sequence",
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

void ApplyAnimation(string name, float frameDuration = 0.1f, bool loop = true)
{
    var sheet = sheets[name];
    var anim = MakeAnimation(sheet.FrameCount, frameDuration: frameDuration, loop: loop);
    world.AddComponent(samurai, sheet);
    world.AddComponent(samurai, anim);
    world.AddComponent(samurai, new AnimationState(0, 0f, false));
    currentSheetName = name;
}

ApplyAnimation("Idle");
world.AddComponent(samurai, new Transform2D(Vector2.Zero, 0f, Vector2.One));

var combatPhase = -1;
var combatSequence = new[] { "Attack_1", "Attack_2", "Attack_3" };

void ApplyCombatSequence()
{
    combatPhase = 0;
    ApplyAnimation(combatSequence[combatPhase], loop: false);
}

// ------------------------------------------------------------------
// Key bindings: switch animations at runtime.
// ------------------------------------------------------------------
Keyboard.AddKeyDown(Keys.W, () => ApplyAnimation("Walk"));
Keyboard.AddKeyDown(Keys.R, () => ApplyAnimation("Run", frameDuration: 0.05f)); // Faster frame rate for running
Keyboard.AddKeyDown(Keys.J, () => ApplyAnimation("Jump", loop: false)); // Don't loop the jump animation
Keyboard.AddKeyDown(Keys.I, () => ApplyAnimation("Idle"));
Keyboard.AddKeyDown(Keys.Num1, () => ApplyAnimation("Attack_1", loop: false)); // Don't loop attack animations
Keyboard.AddKeyDown(Keys.Num2, () => ApplyAnimation("Attack_2", loop: false));
Keyboard.AddKeyDown(Keys.Num3, () => ApplyAnimation("Attack_3", loop: false));
Keyboard.AddKeyDown(Keys.C, ApplyCombatSequence); // Trigger the full combat sequence (for demonstration)
Keyboard.AddKeyDown(Keys.H, () => ApplyAnimation("Hurt", frameDuration: 0.2f, loop: false)); // Slower frame rate for hurt animation
Keyboard.AddKeyDown(Keys.S, () => ApplyAnimation("Shield", frameDuration: 0.2f)); // Slower frame rate for shield animation
Keyboard.AddKeyDown(Keys.D, () => ApplyAnimation("Dead", loop: false)); // Don't loop the dead animation

var currentAnimationLabel = world.CreateEntity();
world.AddComponent(
    currentAnimationLabel,
    new Text($"Current Animation: {currentSheetName}", defaultFont, 24, Color.Green)
);
world.AddComponent(
    currentAnimationLabel,
    new Transform2D(new Vector2(-0.95f, -0.9f), 0f, new Vector2(0.003f, 0.003f))
);

window.OnUpdate += deltaTime => animationSystem.Update((float)deltaTime);
window.OnUpdate += _ =>
{
    // Advance through the combat sequence automatically when each attack finishes
    if (combatPhase < 0)
        return;

    if (world.TryGetComponent<AnimationState>(samurai, out var state) && state.IsFinished)
    {
        combatPhase++;
        if (combatPhase < combatSequence.Length)
            ApplyAnimation(combatSequence[combatPhase], loop: false);
        else
        {
            combatPhase = -1;
            ApplyAnimation("Idle");
        }
    }
};
window.OnUpdate += _ =>
{
    // Update the current animation label text to reflect the active animation
    var textComp = world.GetComponent<Text>(currentAnimationLabel);
    textComp.Content = $"Current Animation: {currentSheetName}";
    world.AddComponent(currentAnimationLabel, textComp); // Re-add to trigger the text system to update the rendering
};
window.OnRender += _ => renderSystem.Render();
window.OnRender += _ => textRenderSystem.Render();
window.OnClosing += () =>
{
    textRenderer.Dispose();
    fontManager.Dispose();
};

window.Run();
