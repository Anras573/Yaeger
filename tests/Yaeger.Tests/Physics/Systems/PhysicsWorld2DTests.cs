using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.Physics.Systems;

public class PhysicsWorld2DTests
{
    [Fact]
    public void Update_ShouldApplyGravityAndMoveEntities()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(0, 10)));
        world.AddComponent(entity, Velocity2D.Zero);
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -10));

        // Act
        physics.Update(1.0f);

        // Assert — entity should have fallen
        var transform = world.GetComponent<Transform2D>(entity);
        Assert.True(transform.Position.Y < 10);

        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.True(velocity.Linear.Y < 0);
    }

    [Fact]
    public void Update_ShouldDetectAndResolveCollisions()
    {
        // Arrange
        var world = new World();

        // Two boxes overlapping, moving towards each other
        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(10, 0));
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, new BoxCollider2D(2, 2));
        world.AddComponent(a, PhysicsMaterial.Default);

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1.5f, 0)));
        world.AddComponent(b, new Velocity2D(-10, 0));
        world.AddComponent(b, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(b, new BoxCollider2D(2, 2));
        world.AddComponent(b, PhysicsMaterial.Default);

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        // Act
        physics.Update(0.0f); // zero dt to avoid movement, just detect existing overlap

        // Assert — collision should have been detected
        Assert.NotEmpty(physics.Manifolds);
    }

    [Fact]
    public void Update_ShouldFireCollisionEvents()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, Velocity2D.Zero);
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, Velocity2D.Zero);
        world.AddComponent(b, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var firedEvents = new List<CollisionManifold>();
        physics.OnCollision += manifold => firedEvents.Add(manifold);

        // Act
        physics.Update(0.0f);

        // Assert
        Assert.Single(firedEvents);
        Assert.Equal(a, firedEvents[0].EntityA);
        Assert.Equal(b, firedEvents[0].EntityB);
    }

    [Fact]
    public void Gravity_ShouldBeConfigurable()
    {
        // Arrange
        var world = new World();
        var physics = new PhysicsWorld2D(world);

        // Act
        physics.Gravity = new Vector2(0, -20);

        // Assert
        Assert.Equal(new Vector2(0, -20), physics.Gravity);
    }

    [Fact]
    public void Constructor_ShouldDefaultToEarthGravity()
    {
        // Arrange & Act
        var world = new World();
        var physics = new PhysicsWorld2D(world);

        // Assert
        Assert.Equal(new Vector2(0, -9.81f), physics.Gravity);
    }

    [Fact]
    public void Update_StaticFloor_DynamicBall_ShouldBounce()
    {
        // Arrange
        var world = new World();

        // Floor (static, wide box at y = 0, height 2, so top edge at y = 1)
        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floor, Velocity2D.Zero);
        world.AddComponent(floor, RigidBody2D.CreateStatic());
        world.AddComponent(floor, new BoxCollider2D(100, 2));
        world.AddComponent(floor, PhysicsMaterial.Bouncy);

        // Ball (dynamic, overlapping with floor — center at y=1.3, radius 0.5, bottom at y=0.8)
        // Floor top edge at y=1, so overlap = 1.0 - 0.8 = 0.2
        var ball = world.CreateEntity();
        world.AddComponent(ball, new Transform2D(new Vector2(0, 1.3f)));
        world.AddComponent(ball, new Velocity2D(0, -10));
        world.AddComponent(ball, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(ball, new CircleCollider2D(0.5f));
        world.AddComponent(ball, PhysicsMaterial.Bouncy);

        var physics = new PhysicsWorld2D(world, Vector2.Zero); // No gravity for simplicity

        // Act
        physics.Update(0.001f); // Very small step to minimize movement before collision

        // Assert — ball should have bounced (velocity Y should be positive or less negative)
        var ballVel = world.GetComponent<Velocity2D>(ball);
        // The ball was going down at -10, after bouncing off a bouncy floor it should reverse
        Assert.True(ballVel.Linear.Y > -10);
    }
}
