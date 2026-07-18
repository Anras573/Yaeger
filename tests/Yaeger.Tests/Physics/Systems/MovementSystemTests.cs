using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class MovementSystemTests
{
    [Fact]
    public void Update_ShouldIntegrateLinearVelocityIntoPosition()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(10, 5));
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(10, transform.Position.X);
        Assert.Equal(5, transform.Position.Y);
    }

    [Fact]
    public void Update_ShouldIntegrateAngularVelocityIntoRotation()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(Vector2.Zero, MathF.PI));
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(MathF.PI, transform.Rotation, 0.001f);
    }

    [Fact]
    public void Update_ShouldScaleByDeltaTime()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(100, 200));
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var system = new MovementSystem(world);

        // Act
        system.Update(0.5f);

        // Assert
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(50, transform.Position.X);
        Assert.Equal(100, transform.Position.Y);
    }

    [Fact]
    public void Update_ShouldNotMoveStaticBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(5, 5)));
        world.AddComponent(entity, new Velocity2D(10, 10));
        world.AddComponent(entity, RigidBody2D.CreateStatic());

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(5, transform.Position.X);
        Assert.Equal(5, transform.Position.Y);
    }

    [Fact]
    public void Update_ShouldMoveKinematicBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(5, 0));
        world.AddComponent(entity, RigidBody2D.CreateKinematic());

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.Equal(5, transform.Position.X);
    }

    [Fact]
    public void Update_ShouldApplyLinearDrag()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(100, 0));
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f, linearDrag: 0.5f));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert — velocity should be reduced by drag before integration
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.True(velocity.Linear.X < 100);
        Assert.True(velocity.Linear.X > 0);
    }

    [Fact]
    public void Update_ShouldNotApplyDragToKinematicBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(100, 0));
        var body = RigidBody2D.CreateKinematic();
        body.LinearDrag = 0.5f; // Set drag, but it shouldn't apply to kinematic
        world.AddComponent(entity, body);

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(100, velocity.Linear.X);
    }

    [Fact]
    public void Update_WithHighDragAndLargeDeltaTime_ShouldClampDragFactorToZero()
    {
        // Arrange — drag * deltaTime > 1 should not invert velocity
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, new Velocity2D(100, 50));
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f, linearDrag: 2.0f));

        var system = new MovementSystem(world);

        // Act — large deltaTime causes drag factor to go negative without clamping
        system.Update(1.0f); // dragFactor = 1 - 2*1 = -1, should be clamped to 0

        // Assert — velocity should be zeroed, not inverted
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(0.0f, velocity.Linear.X);
        Assert.Equal(0.0f, velocity.Linear.Y);
    }

    // ── Tunneling prevention ──────────────────────────────────────────────────

    [Fact]
    public void Update_FastDynamicBody_ShouldNotTunnelThroughThinStaticWall()
    {
        // Arrange — a thin wall (0.1 units thick) at x = 10, and a body moving so fast that a
        // naive single-step integration (1000 units/s for a full second) would land it at
        // x = 1000, straight through the wall's far side.
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(wall, new BoxCollider2D(0.1f, 10));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert — stopped just short of the wall instead of passing through it. The wall's
        // near face is at x = 9.95, so the mover's center (half-width 0.5) should rest around
        // x = 9.45, not anywhere near the naive x = 1000.
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.True(position.X < 9.5f, $"Expected to stop before the wall, got x={position.X}");
        Assert.True(position.X > 9.0f, $"Expected to reach near the wall, got x={position.X}");

        // Velocity itself is untouched by the clamp — next step's discrete detection is what
        // actually resolves the contact (impulse, positional correction).
        var velocity = world.GetComponent<Velocity2D>(mover);
        Assert.Equal(1000f, velocity.Linear.X);
    }

    [Fact]
    public void Update_FastDynamicBodyWithNoObstacles_ShouldIntegrateNormally()
    {
        // Arrange — a body with a BoxCollider2D but nothing in its path should move exactly as
        // far as its velocity says, same as a body with no collider at all.
        var world = new World();

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }

    [Fact]
    public void Update_ObstacleOnNonMatchingLayer_ShouldNotClampMovement()
    {
        // Arrange — the wall only collides with layer 1; the mover is on layer 0 with its
        // default "collides with everything" mask, but the symmetric check still excludes it.
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(wall, new BoxCollider2D(0.1f, 10, layer: 1, collidesWith: 1u << 1));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert — passes straight through; the layer mismatch excludes the wall entirely.
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }

    [Fact]
    public void Update_TriggerObstacle_ShouldNotClampMovement()
    {
        // Arrange
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(wall, new BoxCollider2D(0.1f, 10, isTrigger: true));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert — triggers never block movement, swept or otherwise.
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }

    [Fact]
    public void Update_OneWayObstacle_ShouldNotClampMovement()
    {
        // Arrange — one-way platforms are excluded from the sweep entirely (out of scope for
        // this pass; see docs/physics.md).
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(0.1f, 10, oneWay: true));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }

    [Fact]
    public void Update_DynamicObstacle_ShouldNotClampMovement()
    {
        // Arrange — dynamic-vs-dynamic tunneling prevention is out of scope for this pass; only
        // static/kinematic obstacles participate in the sweep.
        var world = new World();

        var other = world.CreateEntity();
        world.AddComponent(other, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(other, Velocity2D.Zero);
        world.AddComponent(other, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(other, new BoxCollider2D(0.1f, 10));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }

    [Fact]
    public void Update_FastKinematicMover_ShouldNotBeClampedByTunnelingCheck()
    {
        // Arrange — the sweep only applies to Dynamic movers; a manually-driven Kinematic body
        // (e.g. a moving platform) is left to move exactly as its velocity dictates.
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(wall, new BoxCollider2D(0.1f, 10));

        var mover = world.CreateEntity();
        world.AddComponent(mover, new Transform2D(Vector2.Zero));
        world.AddComponent(mover, new Velocity2D(1000, 0));
        world.AddComponent(mover, RigidBody2D.CreateKinematic());
        world.AddComponent(mover, new BoxCollider2D(1, 1));

        var system = new MovementSystem(world);

        // Act
        system.Update(1.0f);

        // Assert
        var position = world.GetComponent<Transform2D>(mover).Position;
        Assert.Equal(1000f, position.X);
    }
}
