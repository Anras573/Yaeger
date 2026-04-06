using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class GravitySystemTests
{
    [Fact]
    public void Update_ShouldApplyGravityToDynamicBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(entity, Velocity2D.Zero);

        var gravity = new Vector2(0, -10.0f);
        var system = new GravitySystem(world, gravity);

        // Act
        system.Update(1.0f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(0, velocity.Linear.X);
        Assert.Equal(-10.0f, velocity.Linear.Y);
    }

    [Fact]
    public void Update_ShouldRespectGravityScale()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f, gravityScale: 0.5f));
        world.AddComponent(entity, Velocity2D.Zero);

        var gravity = new Vector2(0, -10.0f);
        var system = new GravitySystem(world, gravity);

        // Act
        system.Update(1.0f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-5.0f, velocity.Linear.Y);
    }

    [Fact]
    public void Update_ShouldNotAffectStaticBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateStatic());
        world.AddComponent(entity, Velocity2D.Zero);

        var system = new GravitySystem(world, new Vector2(0, -10.0f));

        // Act
        system.Update(1.0f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(Vector2.Zero, velocity.Linear);
    }

    [Fact]
    public void Update_ShouldNotAffectKinematicBodies()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateKinematic());
        world.AddComponent(entity, Velocity2D.Zero);

        var system = new GravitySystem(world, new Vector2(0, -10.0f));

        // Act
        system.Update(1.0f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(Vector2.Zero, velocity.Linear);
    }

    [Fact]
    public void Update_ShouldAccumulateGravityOverMultipleSteps()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(entity, Velocity2D.Zero);

        var system = new GravitySystem(world, new Vector2(0, -10.0f));

        // Act
        system.Update(0.5f);
        system.Update(0.5f);

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-10.0f, velocity.Linear.Y);
    }

    [Fact]
    public void Update_ShouldScaleByDeltaTime()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(entity, Velocity2D.Zero);

        var system = new GravitySystem(world, new Vector2(0, -10.0f));

        // Act
        system.Update(0.016f); // ~60fps

        // Assert
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-0.16f, velocity.Linear.Y, 0.001f);
    }
}
