using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class CollisionResolutionSystemTests
{
    [Fact]
    public void Resolve_ShouldSeparateOverlappingDynamicBodies()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(5, 0));
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, PhysicsMaterial.Default);

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(b, new Velocity2D(-5, 0));
        world.AddComponent(b, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(b, PhysicsMaterial.Default);

        var manifold = new CollisionManifold
        {
            EntityA = a,
            EntityB = b,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.5f,
            ContactPoint = new Vector2(0.75f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — velocities should have changed (A should now move left, B should now move right)
        var velA = world.GetComponent<Velocity2D>(a);
        var velB = world.GetComponent<Velocity2D>(b);

        Assert.True(velA.Linear.X < 5); // A should have slowed down or reversed
        Assert.True(velB.Linear.X > -5); // B should have slowed down or reversed
    }

    [Fact]
    public void Resolve_ShouldNotMoveStaticBodies()
    {
        // Arrange
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(wall, Velocity2D.Zero);
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(wall, PhysicsMaterial.Default);

        var ball = world.CreateEntity();
        world.AddComponent(ball, new Transform2D(new Vector2(0.8f, 0)));
        world.AddComponent(ball, new Velocity2D(-10, 0));
        world.AddComponent(ball, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(ball, PhysicsMaterial.Default);

        var manifold = new CollisionManifold
        {
            EntityA = wall,
            EntityB = ball,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.2f,
            ContactPoint = new Vector2(0.5f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — wall should not have moved
        var wallTransform = world.GetComponent<Transform2D>(wall);
        Assert.Equal(0, wallTransform.Position.X);
        Assert.Equal(0, wallTransform.Position.Y);

        // Ball velocity should have changed
        var ballVel = world.GetComponent<Velocity2D>(ball);
        Assert.True(ballVel.Linear.X > -10);
    }

    [Fact]
    public void Resolve_BothStatic_ShouldDoNothing()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, Velocity2D.Zero);
        world.AddComponent(a, RigidBody2D.CreateStatic());

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(0.5f, 0)));
        world.AddComponent(b, Velocity2D.Zero);
        world.AddComponent(b, RigidBody2D.CreateStatic());

        var manifold = new CollisionManifold
        {
            EntityA = a,
            EntityB = b,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.5f,
            ContactPoint = new Vector2(0.25f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — nothing should change
        var transformA = world.GetComponent<Transform2D>(a);
        var transformB = world.GetComponent<Transform2D>(b);
        Assert.Equal(0, transformA.Position.X);
        Assert.Equal(0.5f, transformB.Position.X);
    }

    [Fact]
    public void Resolve_WithBouncyMaterial_ShouldBounce()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(10, 0));
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, PhysicsMaterial.Bouncy);

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, Velocity2D.Zero);
        world.AddComponent(b, RigidBody2D.CreateStatic());
        world.AddComponent(b, PhysicsMaterial.Bouncy);

        var manifold = new CollisionManifold
        {
            EntityA = a,
            EntityB = b,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.1f,
            ContactPoint = new Vector2(0.5f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — with restitution 1.0, ball should bounce back
        var velA = world.GetComponent<Velocity2D>(a);
        Assert.True(velA.Linear.X < 0); // Should have reversed direction
    }

    [Fact]
    public void Resolve_ShouldApplyPositionalCorrection()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(5, 0));
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, PhysicsMaterial.Default);

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(b, new Velocity2D(-5, 0));
        world.AddComponent(b, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(b, PhysicsMaterial.Default);

        var manifold = new CollisionManifold
        {
            EntityA = a,
            EntityB = b,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.5f,
            ContactPoint = new Vector2(0.75f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — positions should have been corrected (pushed apart)
        var transformA = world.GetComponent<Transform2D>(a);
        var transformB = world.GetComponent<Transform2D>(b);

        // A should have moved left (negative correction)
        Assert.True(transformA.Position.X < 0);
        // B should have moved right (positive correction)
        Assert.True(transformB.Position.X > 1.5f);
    }

    [Fact]
    public void Resolve_SeparatingBodies_ShouldStillApplyPositionalCorrection()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(-5, 0)); // Moving away
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, PhysicsMaterial.Default);

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, new Velocity2D(5, 0)); // Moving away
        world.AddComponent(b, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(b, PhysicsMaterial.Default);

        var manifold = new CollisionManifold
        {
            EntityA = a,
            EntityB = b,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.5f,
            ContactPoint = new Vector2(0.5f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — velocities should not change (already separating), but positions should be corrected
        var velA = world.GetComponent<Velocity2D>(a);
        var velB = world.GetComponent<Velocity2D>(b);
        Assert.Equal(-5, velA.Linear.X);
        Assert.Equal(5, velB.Linear.X);

        // But positions should have been corrected
        var transformA = world.GetComponent<Transform2D>(a);
        var transformB = world.GetComponent<Transform2D>(b);
        Assert.True(transformA.Position.X < 0);
        Assert.True(transformB.Position.X > 1);
    }
}
