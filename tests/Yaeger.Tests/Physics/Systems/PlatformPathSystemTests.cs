using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class PlatformPathSystemTests
{
    [Fact]
    public void Update_HeadingTowardsTarget_ShouldSetVelocityTowardsIt()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(
            entity,
            new PlatformPath([new Vector2(0, 0), new Vector2(10, 0)], speed: 1f)
        );

        var system = new PlatformPathSystem(world);

        // Act
        system.Update(1f / 60f);

        // Assert — heading straight towards (10, 0) at speed 1.
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(1f, velocity.Linear.X, 0.0001f);
        Assert.Equal(0f, velocity.Linear.Y, 0.0001f);
    }

    [Fact]
    public void Update_ArrivingAtWaypoint_LoopingPath_ShouldAdvanceToNextWithWraparound()
    {
        // Arrange — non-ping-pong path; arriving at the last waypoint should wrap to the first.
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(19.995f, 0)));
        var path = new PlatformPath(
            [new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0)],
            speed: 1f
        )
        {
            CurrentWaypointIndex = 2,
        };
        world.AddComponent(entity, path);

        var system = new PlatformPathSystem(world);

        // Act
        system.Update(1f / 60f);

        // Assert
        var updated = world.GetComponent<PlatformPath>(entity);
        Assert.Equal(0, updated.CurrentWaypointIndex);
    }

    [Fact]
    public void Update_ArrivingAtMiddleWaypoint_ShouldAdvanceToNext()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(9.995f, 0)));
        world.AddComponent(
            entity,
            new PlatformPath([new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0)], speed: 1f)
        );

        var system = new PlatformPathSystem(world);

        // Act
        system.Update(1f / 60f);

        // Assert
        var updated = world.GetComponent<PlatformPath>(entity);
        Assert.Equal(2, updated.CurrentWaypointIndex);
    }

    [Fact]
    public void Update_PingPongArrivingAtLastWaypoint_ShouldReverseDirection()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(19.995f, 0)));
        var path = new PlatformPath(
            [new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0)],
            speed: 1f,
            pingPong: true
        )
        {
            CurrentWaypointIndex = 2,
            MovingForward = true,
        };
        world.AddComponent(entity, path);

        var system = new PlatformPathSystem(world);

        // Act
        system.Update(1f / 60f);

        // Assert
        var updated = world.GetComponent<PlatformPath>(entity);
        Assert.False(updated.MovingForward);
        Assert.Equal(1, updated.CurrentWaypointIndex);
    }

    [Fact]
    public void Update_PingPongArrivingAtFirstWaypoint_ShouldReverseDirection()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(0.005f, 0)));
        var path = new PlatformPath(
            [new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0)],
            speed: 1f,
            pingPong: true
        )
        {
            CurrentWaypointIndex = 0,
            MovingForward = false,
        };
        world.AddComponent(entity, path);

        var system = new PlatformPathSystem(world);

        // Act
        system.Update(1f / 60f);

        // Assert
        var updated = world.GetComponent<PlatformPath>(entity);
        Assert.True(updated.MovingForward);
        Assert.Equal(1, updated.CurrentWaypointIndex);
    }

    [Fact]
    public void Update_WithMovementSystem_ShouldPingPongBetweenWaypoints()
    {
        // Arrange — end-to-end: PlatformPathSystem sets velocity, MovementSystem integrates it,
        // same as a game's real update loop would.
        var world = new World();
        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(platform, Velocity2D.Zero);
        world.AddComponent(platform, RigidBody2D.CreateKinematic());
        world.AddComponent(
            platform,
            new PlatformPath([new Vector2(0, 0), new Vector2(5, 0)], speed: 5f, pingPong: true)
        );

        var pathSystem = new PlatformPathSystem(world);
        var movementSystem = new MovementSystem(world);

        var maxX = float.NegativeInfinity;
        var reversedAfterReachingFarEnd = false;

        // Act — 5 simulated seconds, several round trips of a 5-unit path at 5 units/s.
        for (var i = 0; i < 300; i++)
        {
            pathSystem.Update(1f / 60f);
            movementSystem.Update(1f / 60f);

            var x = world.GetComponent<Transform2D>(platform).Position.X;
            if (x > maxX)
                maxX = x;

            if (maxX > 4.9f && x < maxX - 0.5f)
                reversedAfterReachingFarEnd = true;
        }

        // Assert
        Assert.True(maxX > 4.9f, $"Expected to reach near x=5, got max {maxX}");
        Assert.True(
            reversedAfterReachingFarEnd,
            "Expected the platform to turn back after reaching the far end"
        );
    }
}
