using System.Diagnostics;
using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Benchmarks.Physics;

/// <summary>
/// Headless 2D physics benchmark (issue #338). Needs no window or display. Two scenarios:
/// <list type="number">
/// <item><description>
/// <c>count</c> dynamic boxes bouncing around over a large noisy tilemap (thousands of merged
/// static colliders), stepped through <see cref="PhysicsWorld2D"/>.
/// </description></item>
/// <item><description>
/// One <see cref="CharacterController2D"/> walking through a level of a few thousand static
/// <see cref="BoxCollider2D"/>s via <see cref="CharacterControllerSystem"/>.
/// </description></item>
/// </list>
/// Reports mean milliseconds and allocated bytes per update.
/// </summary>
public sealed class PhysicsBenchmark : IBenchmark
{
    private const int Steps = 300;
    private const int WarmupSteps = 30;
    private const float Dt = 1f / 120f;

    public string Arg => "physics";

    public string Description =>
        "Headless 2D physics: dynamic boxes over a big tilemap, and a character controller among thousands of static colliders (issue #338).";

    public int DefaultCount => 500;

    public void Run(int count)
    {
        RunTilemapScenario(count);
        RunControllerScenario(3000);
    }

    private static void RunTilemapScenario(int dynamicBoxes)
    {
        var world = new World();
        const int width = 200;
        const int height = 60;
        var rng = new Random(1234);

        // Noisy terrain: a solid base with scattered pillars and floating blocks, so the
        // greedy merge yields thousands of rectangles rather than a handful.
        var tiles = new int[width * height];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = -1;
        for (var x = 0; x < width; x++)
        {
            var groundTop = 8 + (int)(4 * MathF.Sin(x * 0.3f)) + rng.Next(0, 2);
            for (var y = 0; y < groundTop; y++)
                tiles[(height - 1 - y) * width + x] = 0;
        }
        for (var n = 0; n < 2500; n++)
            tiles[rng.Next(0, height - 14) * width + rng.Next(0, width)] = 0;

        var tileset = new Tileset("Assets/tiles.png", columns: 1, solidTileIndices: [0]);
        var map = world.CreateEntity();
        world.AddComponent(map, new Transform2D(Vector2.Zero));
        world.AddComponent(map, new Tilemap(tileset, width, height, tiles));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -20f), broadphaseCellSize: 2f);

        for (var i = 0; i < dynamicBoxes; i++)
        {
            var e = world.CreateEntity();
            world.AddComponent(
                e,
                new Transform2D(new Vector2(rng.Next(2, width - 2), rng.Next(height - 12, height)))
            );
            world.AddComponent(e, new BoxCollider2D(new Vector2(0.8f, 0.8f)));
            world.AddComponent(e, RigidBody2D.CreateDynamic(1f));
            world.AddComponent(
                e,
                new Velocity2D(new Vector2(rng.Next(-10, 10), rng.Next(-5, 5)), 0f)
            );
        }

        physics.Update(0f); // generate tilemap colliders
        var colliderCount = world.Query<BoxCollider2D, Transform2D>().Count();
        var (ms, bytes) = Measure(() => physics.Update(Dt));
        Console.WriteLine(
            $"[tilemap] {dynamicBoxes} dynamic boxes, {colliderCount - dynamicBoxes} static colliders: "
                + $"{ms:F3} ms/step, {bytes:N0} B/step"
        );
    }

    private static void RunControllerScenario(int staticColliders)
    {
        var world = new World();
        var system = new CharacterControllerSystem(world, new Vector2(0, -30f));
        var rng = new Random(99);

        // A long ground strip plus scattered platforms/walls spread across the level.
        var ground = world.CreateEntity();
        world.AddComponent(ground, new Transform2D(new Vector2(0, -1)));
        world.AddComponent(ground, new BoxCollider2D(new Vector2(100_000f, 2f)));
        for (var i = 1; i < staticColliders; i++)
        {
            var e = world.CreateEntity();
            world.AddComponent(
                e,
                new Transform2D(new Vector2(rng.Next(-1000, 2000), rng.Next(2, 40)))
            );
            world.AddComponent(e, new BoxCollider2D(new Vector2(3f, 0.5f)));
            world.AddComponent(e, RigidBody2D.CreateStatic());
        }

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 1.5f)));
        world.AddComponent(player, new Velocity2D(new Vector2(6f, 0f), 0f));
        world.AddComponent(player, new CharacterController2D(new Vector2(0.8f, 1.6f)));

        var (ms, bytes) = Measure(() => system.Update(Dt));
        Console.WriteLine(
            $"[controller] 1 controller, {staticColliders} static colliders: "
                + $"{ms:F3} ms/update, {bytes:N0} B/update"
        );
    }

    private static (double Ms, long Bytes) Measure(Action step)
    {
        for (var i = 0; i < WarmupSteps; i++)
            step();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Steps; i++)
            step();
        sw.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return (sw.Elapsed.TotalMilliseconds / Steps, allocated / Steps);
    }
}
