using System.Numerics;
using Benchmarks.Sprites.Components;
using Benchmarks.Sprites.Systems;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace Benchmarks.Sprites;

/// <summary>
/// Rendering stress test: submits many sprites sharing a single texture so the renderer flushes
/// them as one batched draw call per frame. FPS is a proxy for CPU-side submission cost (ECS
/// iteration + per-vertex transform), not GPU fill rate — see <c>docs/instancing.md</c>'s sibling
/// benchmarks for GPU draw-call cost instead.
/// </summary>
public sealed class SpritesBenchmark : IBenchmark
{
    private const string TexturePath = "Assets/Sprites/square.png";

    public string Arg => "sprites";
    public string Description => "Sprite-batching stress test (many sprites, one shared texture)";
    public int DefaultCount => 5_000;

    public void Run(int count)
    {
        using var window = Window.Create();

        var world = new World();

        var renderer = new Renderer(window);
        var renderSystem = new UnifiedRenderSystem(renderer, null, world);
        var physicsSystem = new PhysicsSystem(world);

        var random = new Random(42);
        for (var i = 0; i < count; i++)
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
            world.AddComponent(entity, new Sprite(TexturePath));
        }

        Console.WriteLine($"Sprites benchmark - {count} sprites, single shared texture");
        Console.WriteLine("Press ESC to exit");

        var stats = new FrameStats("Sprites");

        Keyboard.AddKeyDown(Keys.Escape, window.Close);

        window.OnUpdate += delta => physicsSystem.Update((float)delta);
        window.OnRender += delta =>
        {
            stats.Tick(delta, () => $"{count} sprites");
            renderSystem.Render();
        };

        window.Run();
    }
}
