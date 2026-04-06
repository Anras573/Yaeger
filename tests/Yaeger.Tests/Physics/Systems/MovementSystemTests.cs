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
}
