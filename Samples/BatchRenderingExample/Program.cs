using System.Numerics;

using BatchRenderingExample.Components;
using BatchRenderingExample.Systems;

using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// This example demonstrates batch rendering in Yaeger.
// 
// Batch rendering groups draw calls by texture to minimize GPU state changes,
// which is essential for efficient font rendering and rendering many sprites.
// 
// Press SPACE to toggle between batch rendering and individual rendering.
// Press ESC to exit.

const int spriteCount = 500;
const string texturePath = "Assets/square.png";

bool useBatchRendering = true;

using var window = Window.Create();

var world = new World();

var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);

var batchRenderer = new BatchRenderer(window);
var batchRenderSystem = new BatchRenderSystem(batchRenderer, world);

var physicsSystem = new PhysicsSystem(world);

var random = new Random(42);
for (int i = 0; i < spriteCount; i++)
{
    var velocity = new Velocity(new Vector2(
        (float)random.NextDouble() * 0.5f - 0.25f,
        (float)random.NextDouble() * 0.5f - 0.25f
    ));

    var position = new Vector2(
        (float)random.NextDouble() * 2f - 1f,
        (float)random.NextDouble() * 2f - 1f
    );

    var rotationSpeed = new RotationSpeed((float)random.NextDouble() * 2f - 1f);
    var rotation = (float)random.NextDouble() * MathF.PI * 2f;
    var scale = new Vector2((float)random.NextDouble() * 0.05f + 0.02f);

    var entity = world.CreateEntity();
    world.AddComponent(entity, velocity);
    world.AddComponent(entity, rotationSpeed);
    world.AddComponent(entity, new Transform2D(position, rotation, scale));
    world.AddComponent(entity, new Sprite(texturePath));
}

Console.WriteLine($"Batch Rendering Example - Rendering {spriteCount} sprites");
Console.WriteLine("Press SPACE to toggle between batch and individual rendering");
Console.WriteLine("Press ESC to exit");

int fps = 0;
double lastFpsTime = 0;

// Toggle rendering mode
Keyboard.AddKeyDown(Keys.Space, () =>
{
    useBatchRendering = !useBatchRendering;
    Console.WriteLine($"Switched to {(useBatchRendering ? "BATCH" : "INDIVIDUAL")} rendering");
});

Keyboard.AddKeyDown(Keys.Escape, () =>
{
    window.Close();
});

window.OnUpdate += Update;
window.OnRender += Render;
window.Run();
return;

void Update(double deltaTime)
{
    physicsSystem.Update((float)deltaTime);
}

void Render(double deltaTime)
{
    fps++;
    lastFpsTime += deltaTime;

    if (lastFpsTime >= 1.0)
    {
        Console.WriteLine($"FPS: {fps} ({(useBatchRendering ? "BATCH" : "INDIVIDUAL")} rendering, {spriteCount} sprites)");
        fps = 0;
        lastFpsTime = 0;
    }

    if (useBatchRendering)
    {
        batchRenderSystem.Render();
    }
    else
    {
        renderSystem.Render();
    }
}