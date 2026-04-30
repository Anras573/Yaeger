using System.Numerics;
using RenderingStressTest.Components;
using RenderingStressTest.Systems;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Rendering stress test: submits many sprites sharing a single texture so the
// renderer flushes them as one batched draw call per frame. FPS is printed to
// the console once per second so perf regressions in the renderer show up here.
//
// Press ESC to exit.

const int spriteCount = 5_000;
const string texturePath = "Assets/square.png";

using var window = Window.Create();

var world = new World();

var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);
var physicsSystem = new PhysicsSystem(world);

var random = new Random(42);
for (var i = 0; i < spriteCount; i++)
{
    var velocity = new Velocity(
        new Vector2(
            (float)random.NextDouble() * 0.5f - 0.25f,
            (float)random.NextDouble() * 0.5f - 0.25f
        )
    );

    var position = new Vector2(
        (float)random.NextDouble() * 2f - 1f,
        (float)random.NextDouble() * 2f - 1f
    );

    var rotationSpeed = new RotationSpeed((float)random.NextDouble() * 2f - 1f);
    var rotation = (float)random.NextDouble() * MathF.PI * 2f;
    var scale = new Vector2((float)random.NextDouble() * 0.03f + 0.01f);

    var entity = world.CreateEntity();
    world.AddComponent(entity, velocity);
    world.AddComponent(entity, rotationSpeed);
    world.AddComponent(entity, new Transform2D(position, rotation, scale));
    world.AddComponent(entity, new Sprite(texturePath));
}

Console.WriteLine($"Rendering Stress Test - {spriteCount} sprites, single shared texture");
Console.WriteLine("Press ESC to exit");

var frameCount = 0;
var secondElapsed = 0.0;

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.OnUpdate += delta => physicsSystem.Update((float)delta);
window.OnRender += delta =>
{
    frameCount++;
    secondElapsed += delta;
    if (secondElapsed >= 1.0)
    {
        Console.WriteLine($"FPS: {frameCount} ({spriteCount} sprites)");
        frameCount = 0;
        secondElapsed = 0;
    }

    renderSystem.Render();
};

window.Run();
