using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics.Components;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Camera demo: pan/zoom/rotate a 3x3 grid of world-space sprites while a screen-space HUD
// stays pinned to the top. Demonstrates the opt-in Camera2D flow and the sprite/text split
// (Renderer uses the camera, TextRenderer is always screen-space), plus CameraFollowSystem's
// smoothing/deadzone/look-ahead and CameraBounds clamping.
//
// Controls (manual mode, the default):
//   WASD   — pan camera
//   Q / E  — zoom out / in
//   ← / →  — rotate camera
//   R      — reset camera
//   Space  — toggle follow mode
//   ESC    — exit
//
// Controls (follow mode):
//   WASD   — move the red target; the camera tracks it (smoothing, deadzone, look-ahead),
//            clamped to the level bounds
//   Q / E  — zoom out / in (bounds clamping adjusts with it)
//   ← / →  — rotate camera
//   Space  — back to manual mode

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var renderSystem = new UnifiedRenderSystem(renderer, textRenderer, world, window);
var cameraFollowSystem = new CameraFollowSystem(world, window);

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

// Follow target — a distinct red square the player moves around in follow mode.
var targetEntity = world.CreateEntity();
world.AddComponent(targetEntity, new Sprite(sprite, Color.Red));
world.AddComponent(targetEntity, new Transform2D(Vector2.Zero, 0f, new Vector2(0.12f)));
world.AddComponent(targetEntity, Velocity2D.Zero); // read by CameraFollowSystem for look-ahead

// Camera entity — tagged so we can look it up for updates.
const string cameraTag = "camera";
var cameraEntity = world.CreateEntity(cameraTag);
world.AddComponent(cameraEntity, new Camera2D());

// A level-bounds rectangle wider than the sprite grid, so panning/following the target near an
// edge visibly clamps instead of showing past it. CameraBounds only has an effect when a
// CameraFollow is also present on the same entity — it's inert here in manual mode.
world.AddComponent(cameraEntity, new CameraBounds(new Vector2(-3, -3), new Vector2(3, 3)));

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
    new Text(
        "WASD pan/move   Q/E zoom   arrows rotate   R reset   space toggle follow",
        font,
        16,
        Color.White
    )
);
world.AddComponent(
    hudControlsEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.82f), Scale = new Vector2(0.003f) }
);

const float panSpeed = 1.0f;
const float zoomSpeed = 1.5f;
const float rotationSpeed = 2.0f;
const float targetMoveSpeed = 1.5f;

var followEnabled = false;

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(
    Keys.R,
    () =>
    {
        world.AddComponent(cameraEntity, new Camera2D());
    }
);
Keyboard.AddKeyDown(
    Keys.Space,
    () =>
    {
        followEnabled = !followEnabled;
        if (followEnabled)
        {
            world.AddComponent(
                cameraEntity,
                new CameraFollow(
                    targetEntity,
                    smoothing: 5f,
                    deadzoneHalfExtents: new Vector2(0.3f, 0.3f),
                    lookAheadTime: 0.15f
                )
            );
        }
        else
        {
            world.RemoveComponent<CameraFollow>(cameraEntity);
        }
    }
);

window.OnUpdate += Update;
window.OnRender += _ =>
{
    renderSystem.Render();
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

    var input = Vector2.Zero;
    if (Keyboard.IsKeyPressed(Keys.W))
        input.Y += 1f;
    if (Keyboard.IsKeyPressed(Keys.S))
        input.Y -= 1f;
    if (Keyboard.IsKeyPressed(Keys.A))
        input.X -= 1f;
    if (Keyboard.IsKeyPressed(Keys.D))
        input.X += 1f;
    var direction = input == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(input);

    if (followEnabled)
    {
        // WASD moves the target instead of the camera; the camera itself is driven by
        // CameraFollowSystem below. Velocity2D is updated too so look-ahead has something to
        // read, even though movement here is direct position assignment rather than physics.
        var targetTransform = world.GetComponent<Transform2D>(targetEntity);
        targetTransform.Position += direction * targetMoveSpeed * dt;
        world.AddComponent(targetEntity, targetTransform);
        world.AddComponent(targetEntity, new Velocity2D(direction * targetMoveSpeed));
    }
    else if (direction != Vector2.Zero)
    {
        camera.Position += direction * panSpeed * dt / camera.Zoom;
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

    if (followEnabled)
        cameraFollowSystem.Update(dt);

    camera = world.GetComponent<Camera2D>(cameraEntity);
    var mode = followEnabled ? "follow" : "manual";
    var state = new Text(
        $"[{mode}]  pos ({camera.Position.X:F2}, {camera.Position.Y:F2})   zoom {camera.Zoom:F2}   rot {camera.Rotation:F2}",
        font,
        16,
        Color.White
    );
    world.AddComponent(hudStateEntity, state);
}
