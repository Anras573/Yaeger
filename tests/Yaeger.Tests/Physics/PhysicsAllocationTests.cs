using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics;

public class PhysicsAllocationTests
{
    private static long MeasureAllocatedBytes(Action action, int warmup, int iterations)
    {
        for (var i = 0; i < warmup; i++)
            action();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
            action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void PhysicsWorldUpdate_SteadyState_ShouldAllocateNothing()
    {
        // Arrange — a tilemap floor, boxes and circles resting/bouncing on it, a one-way platform
        // with an entity mid drop-through, and a kinematic platform: every subsystem is active.
        var world = new World();
        var tileset = new Tileset("Assets/tiles.png", columns: 1, solidTileIndices: [0]);
        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(-8, -1)));
        world.AddComponent(floor, new Tilemap(tileset, 16, 1, new int[16]));

        for (var i = 0; i < 6; i++)
        {
            var box = world.CreateEntity();
            world.AddComponent(box, new Transform2D(new Vector2(-5 + i * 2, 1 + i * 0.5f)));
            world.AddComponent(box, new BoxCollider2D(new Vector2(1, 1)));
            world.AddComponent(box, RigidBody2D.CreateDynamic(1f));
            world.AddComponent(box, new Velocity2D(new Vector2(i - 3, 0)));
        }

        var ball = world.CreateEntity();
        world.AddComponent(ball, new Transform2D(new Vector2(0, 3)));
        world.AddComponent(ball, new CircleCollider2D(0.5f));
        world.AddComponent(ball, RigidBody2D.CreateDynamic(1f));
        world.AddComponent(ball, new Velocity2D(Vector2.Zero));

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, new BoxCollider2D(new Vector2(3, 0.2f)) { OneWay = true });
        world.AddComponent(platform, RigidBody2D.CreateStatic());

        var physics = new PhysicsWorld2D(world, new Vector2(0, -10));
        physics.DropThrough(ball, duration: 1000f);

        // Act
        var allocated = MeasureAllocatedBytes(() => physics.Update(1f / 120f), 600, 600);

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void CharacterControllerUpdate_SteadyState_ShouldAllocateNothing()
    {
        // Arrange — a controller walking on ground, with a trigger zone it overlaps.
        var world = new World();
        var ground = world.CreateEntity();
        world.AddComponent(ground, new Transform2D(new Vector2(0, -1)));
        world.AddComponent(ground, new BoxCollider2D(new Vector2(1000, 2)));

        var zone = world.CreateEntity();
        world.AddComponent(zone, new Transform2D(new Vector2(0, 1)));
        world.AddComponent(zone, new BoxCollider2D(new Vector2(1000, 2)) { IsTrigger = true });

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 0.8f)));
        world.AddComponent(player, new Velocity2D(new Vector2(1, 0)));
        world.AddComponent(player, new CharacterController2D(new Vector2(0.8f, 1.6f)));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        // Act
        var allocated = MeasureAllocatedBytes(() => system.Update(1f / 120f), 100, 600);

        // Assert
        Assert.Equal(0, allocated);
    }
}
