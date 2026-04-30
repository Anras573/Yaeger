using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Camera demo: pan/zoom/rotate a 3x3 grid of world-space sprites while a screen-space HUD
// stays pinned to the top. Demonstrates the opt-in Camera2D flow and the sprite/text split
// (Renderer uses the camera, TextRenderer is always screen-space).
//
// Controls:
//   WASD   — pan camera
//   Q / E  — zoom out / in
//   ← / →  — rotate camera
//   R      — reset camera
//   ESC    — exit

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world, window);

var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var textRenderSystem = new TextRenderSystem(textRenderer, world);

// World sprites: 3x3 grid, visibly spread in world space so camera movement reads clearly.
const string sprite = "Assets/square.png";
for (var row = -1; row <= 1; row++)
{
    for (var col = -1; col <= 1; col++)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite(sprite));
        world.AddComponent(
            entity,
            new Transform2D(new Vector2(col * 0.5f, row * 0.5f), 0f, new Vector2(0.15f))
        );
    }
}

// Camera entity — tagged so we can look it up for updates.
const string cameraTag = "camera";
var cameraEntity = world.CreateEntity(cameraTag);
world.AddComponent(cameraEntity, new Camera2D());

// HUD text — screen-space, stays fixed as camera moves.
// Two stacked lines: a dynamic state readout on top, a static controls hint below.
var font = fontManager.Load("Assets/Roboto-Regular.ttf");

var hudStateEntity = world.CreateEntity("hud-state");
world.AddComponent(hudStateEntity, new Text("", font, 16, Color.White));
world.AddComponent(
    hudStateEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.9f), Scale = new Vector2(0.003f) }
);

var hudControlsEntity = world.CreateEntity("hud-controls");
world.AddComponent(
    hudControlsEntity,
    new Text("WASD pan   Q/E zoom   arrows rotate   R reset", font, 16, Color.White)
);
world.AddComponent(
    hudControlsEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.82f), Scale = new Vector2(0.003f) }
);

const float panSpeed = 1.0f;
const float zoomSpeed = 1.5f;
const float rotationSpeed = 2.0f;

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(
    Keys.R,
    () =>
    {
        world.AddComponent(cameraEntity, new Camera2D());
    }
);

window.OnUpdate += Update;
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
return;

void Update(double deltaTime)
{
    var dt = (float)deltaTime;
    var camera = world.GetComponent<Camera2D>(cameraEntity);

    var pan = Vector2.Zero;
    if (Keyboard.IsKeyPressed(Keys.W))
        pan.Y += 1f;
    if (Keyboard.IsKeyPressed(Keys.S))
        pan.Y -= 1f;
    if (Keyboard.IsKeyPressed(Keys.A))
        pan.X -= 1f;
    if (Keyboard.IsKeyPressed(Keys.D))
        pan.X += 1f;
    if (pan != Vector2.Zero)
    {
        camera.Position += Vector2.Normalize(pan) * panSpeed * dt / camera.Zoom;
    }

    if (Keyboard.IsKeyPressed(Keys.E))
        camera.Zoom *= MathF.Pow(zoomSpeed, dt);
    if (Keyboard.IsKeyPressed(Keys.Q))
        camera.Zoom /= MathF.Pow(zoomSpeed, dt);

    if (Keyboard.IsKeyPressed(Keys.Right))
        camera.Rotation += rotationSpeed * dt;
    if (Keyboard.IsKeyPressed(Keys.Left))
        camera.Rotation -= rotationSpeed * dt;

    world.AddComponent(cameraEntity, camera);

    var state = new Text(
        $"pos ({camera.Position.X:F2}, {camera.Position.Y:F2})   zoom {camera.Zoom:F2}   rot {camera.Rotation:F2}",
        font,
        16,
        Color.White
    );
    world.AddComponent(hudStateEntity, state);
}
