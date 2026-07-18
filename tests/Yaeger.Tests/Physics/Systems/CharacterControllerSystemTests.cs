using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

public class CharacterControllerSystemTests
{
    // ── Grounding ─────────────────────────────────────────────────────────────

    [Fact]
    public void Update_FallingOntoFloor_ShouldLandWithZeroBounceAndBeGrounded()
    {
        // Arrange — a wide floor; the controller starts well above it and falls.
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floor, new BoxCollider2D(10, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        // Act — simulate long enough to fall and settle.
        for (var i = 0; i < 180; i++)
            system.Update(1f / 60f);

        // Assert — resting exactly on top (floor top 0.5 + half-height 0.5), zero velocity, grounded.
        var position = world.GetComponent<Transform2D>(player).Position;
        var velocity = world.GetComponent<Velocity2D>(player);
        var controller = world.GetComponent<CharacterController2D>(player);

        Assert.Equal(1.0f, position.Y, 0.01f);
        Assert.Equal(0f, velocity.Linear.Y);
        Assert.True(controller.IsGrounded);
        Assert.Equal(Vector2.UnitY, controller.GroundNormal);
    }

    [Fact]
    public void Update_RestingOnFloor_ShouldStayGroundedEveryStep()
    {
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floor, new BoxCollider2D(10, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 1.0f)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        for (var i = 0; i < 10; i++)
        {
            system.Update(1f / 60f);
            Assert.True(world.GetComponent<CharacterController2D>(player).IsGrounded);
        }
    }

    [Fact]
    public void Update_NoFloorBelow_ShouldNotBeGrounded()
    {
        var world = new World();

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        system.Update(1f / 60f);

        Assert.False(world.GetComponent<CharacterController2D>(player).IsGrounded);
    }

    // ── Sliding ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_RunningIntoWall_ShouldStopHorizontalButPreserveVertical()
    {
        // Arrange — a tall wall to the right; the controller moves right and falls simultaneously.
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(wall, new BoxCollider2D(1, 10));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(4.0f, 0)));
        world.AddComponent(player, new Velocity2D(5, -3));
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        // Act
        system.Update(0.01f);

        // Assert — horizontal motion stopped dead by the wall...
        var velocity = world.GetComponent<Velocity2D>(player);
        var position = world.GetComponent<Transform2D>(player).Position;
        var controller = world.GetComponent<CharacterController2D>(player);

        Assert.Equal(0f, velocity.Linear.X);
        Assert.Equal(4.0f, position.X, 0.0001f); // pushed back to touching, not past it
        Assert.True(controller.IsTouchingWallRight);
        Assert.False(controller.IsTouchingWallLeft);

        // ...but vertical (falling) motion is untouched by the wall (still falling, not zeroed).
        Assert.True(velocity.Linear.Y < 0f);
    }

    [Fact]
    public void Update_WallToTheLeft_ShouldSetIsTouchingWallLeft()
    {
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(-5, 0)));
        world.AddComponent(wall, new BoxCollider2D(1, 10));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(-4.0f, 0)));
        world.AddComponent(player, new Velocity2D(-5, 0));
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        system.Update(0.01f);

        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.True(controller.IsTouchingWallLeft);
        Assert.False(controller.IsTouchingWallRight);
        Assert.Equal(0f, world.GetComponent<Velocity2D>(player).Linear.X);
    }

    // ── Ceiling ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_HittingCeiling_ShouldZeroUpwardVelocityAndSetFlag()
    {
        var world = new World();

        var ceiling = world.CreateEntity();
        world.AddComponent(ceiling, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(ceiling, new BoxCollider2D(10, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 4.0f)));
        world.AddComponent(player, new Velocity2D(0, 8));
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        system.Update(0.01f);

        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.True(controller.IsTouchingCeiling);
        Assert.False(controller.IsGrounded);
        Assert.Equal(0f, world.GetComponent<Velocity2D>(player).Linear.Y);
    }

    // ── Depenetration ─────────────────────────────────────────────────────────

    [Fact]
    public void Update_SpawnedEmbeddedInFloor_ShouldDepenetrateUpwardNotSideways()
    {
        // Arrange — a wide, thin floor; the controller spawns overlapping it from above (not
        // moving), which must push it straight up, never sideways.
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floor, new BoxCollider2D(10, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 0.3f)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        // Act — dt = 0 isolates depenetration from any velocity integration.
        system.Update(0f);

        // Assert
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.Equal(0f, position.X, 0.0001f);
        Assert.Equal(1.0f, position.Y, 0.0001f);
        Assert.True(world.GetComponent<CharacterController2D>(player).IsGrounded);
    }

    // ── Seam crossing ─────────────────────────────────────────────────────────

    [Fact]
    public void Update_RunningAcrossSeamBetweenAdjacentColliders_ShouldNeverSnag()
    {
        // Arrange — two adjacent, same-height floor colliders (as if two merged tilemap
        // rectangles meeting at x = 5), and a controller resting on top moving across the seam.
        var world = new World();

        var floorLeft = world.CreateEntity();
        world.AddComponent(floorLeft, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floorLeft, new BoxCollider2D(10, 1));

        var floorRight = world.CreateEntity();
        world.AddComponent(floorRight, new Transform2D(new Vector2(10, 0)));
        world.AddComponent(floorRight, new BoxCollider2D(10, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(3, 1.0f))); // resting on top
        world.AddComponent(player, new Velocity2D(2, 0));
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        // Act — run across the seam at x = 5 (and the far collider's own right edge at x = 15).
        for (var i = 0; i < 240; i++) // 4 seconds @ 2 units/s => travels 8 units, well past x=5
        {
            system.Update(1f / 60f);

            var controller = world.GetComponent<CharacterController2D>(player);
            var y = world.GetComponent<Transform2D>(player).Position.Y;

            // Assert — never snags: stays grounded, Y never drops, and no phantom wall contact.
            Assert.True(controller.IsGrounded);
            Assert.Equal(1.0f, y, 0.01f);
            Assert.False(controller.IsTouchingWallLeft);
            Assert.False(controller.IsTouchingWallRight);
        }

        Assert.True(world.GetComponent<Transform2D>(player).Position.X > 5f);
    }

    // ── One-way platforms ─────────────────────────────────────────────────────

    [Fact]
    public void Update_OneWayPlatform_JumpingThroughFromBelow_ShouldPassThrough()
    {
        // Arrange — start close enough below the platform (which spans y [1.75, 2.25]) that,
        // with a small step size, the controller actually overlaps it for several frames while
        // rising — exercising the one-way filter itself, not just tunneling clean over it.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, new BoxCollider2D(new Vector2(10, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 1.6f)));
        world.AddComponent(player, new Velocity2D(0, 8)); // jumping up through it
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        // Act — small steps so the controller genuinely overlaps the platform band en route.
        for (var i = 0; i < 20; i++) // 0.2s @ 8 units/s => travels 1.6 units total
            system.Update(0.01f);

        // Assert — passed cleanly through and above the platform, never treated as ground.
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.True(position.Y > 2.5f);
        Assert.False(world.GetComponent<CharacterController2D>(player).IsGrounded);
    }

    [Fact]
    public void Update_OneWayPlatform_LandingFromAbove_ShouldRestOnTop()
    {
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, new BoxCollider2D(new Vector2(10, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        for (var i = 0; i < 120; i++)
            system.Update(1f / 60f);

        // Platform top = 2 + 0.25 = 2.25; resting center = 2.25 + 0.5 = 2.75.
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.Equal(2.75f, position.Y, 0.01f);
        Assert.True(world.GetComponent<CharacterController2D>(player).IsGrounded);
    }

    // ── Step-up ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShortLedgeWithinStepHeight_ShouldClimbInsteadOfStopping()
    {
        // Arrange — a 0.3-unit curb, and a controller with a 0.5 step height running into it.
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(-2.5f, 0)));
        world.AddComponent(floor, new BoxCollider2D(5, 1)); // top = 0.5, spans x [-5, 0]

        var curb = world.CreateEntity();
        world.AddComponent(curb, new Transform2D(new Vector2(2.5f, 0.3f)));
        world.AddComponent(curb, new BoxCollider2D(5, 1)); // top = 0.8, spans x [0, 5]

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(-0.3f, 1.0f))); // resting on floor
        world.AddComponent(player, new Velocity2D(2, 0));
        world.AddComponent(player, new CharacterController2D(1, 1, stepHeight: 0.5f));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        // Act
        for (var i = 0; i < 60; i++) // 1 second — plenty to cross onto the curb and settle
            system.Update(1f / 60f);

        // Assert — climbed onto the curb (resting at its top, 0.8 + half-height 0.5 = 1.3),
        // not blocked by it.
        var controller = world.GetComponent<CharacterController2D>(player);
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.True(position.X > 0.3f);
        Assert.Equal(1.3f, position.Y, 0.05f);
        Assert.False(controller.IsTouchingWallRight);
    }

    [Fact]
    public void Update_TallObstacleExceedingStepHeight_ShouldBlockNormally()
    {
        // Arrange — a full-height wall, far taller than the controller's step height.
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(-2.5f, 0)));
        world.AddComponent(floor, new BoxCollider2D(5, 1));

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(1, 5)));
        world.AddComponent(wall, new BoxCollider2D(1, 10)); // spans y [0, 10] — far above step height

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(-0.3f, 1.0f)));
        world.AddComponent(player, new Velocity2D(2, 0));
        world.AddComponent(player, new CharacterController2D(1, 1, stepHeight: 0.5f));

        var system = new CharacterControllerSystem(world, new Vector2(0, -20));

        for (var i = 0; i < 60; i++)
            system.Update(1f / 60f);

        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.True(controller.IsTouchingWallRight);
        Assert.Equal(1.0f, world.GetComponent<Transform2D>(player).Position.Y, 0.01f);
    }

    // ── Layer / mask filtering ────────────────────────────────────────────────

    [Fact]
    public void Update_ObstacleOnNonMatchingLayer_ShouldBeIgnored()
    {
        var world = new World();

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(wall, new BoxCollider2D(1, 10, layer: 1, collidesWith: 1u << 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(4.5f, 0)));
        world.AddComponent(player, new Velocity2D(5, 0));
        world.AddComponent(
            player,
            new CharacterController2D(1, 1, layer: 0, collidesWith: 1u << 0)
        );

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        system.Update(0.1f);

        // Assert — the controller's mask doesn't include the wall's layer, so it passes through.
        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.False(controller.IsTouchingWallRight);
        Assert.Equal(5f, world.GetComponent<Velocity2D>(player).Linear.X);
    }

    [Fact]
    public void Update_TriggerCollider_ShouldBeIgnoredForMovement()
    {
        var world = new World();

        var sensor = world.CreateEntity();
        world.AddComponent(sensor, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(sensor, new BoxCollider2D(1, 10, isTrigger: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(4.5f, 0)));
        world.AddComponent(player, new Velocity2D(5, 0));
        world.AddComponent(player, new CharacterController2D(1, 1));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        system.Update(0.1f);

        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.False(controller.IsTouchingWallRight);
        Assert.Equal(5f, world.GetComponent<Velocity2D>(player).Linear.X);
    }

    // ── GravityScale independence ─────────────────────────────────────────────

    [Fact]
    public void Update_GravityScale_ShouldScaleControllerGravityIndependently()
    {
        var world = new World();

        var unscaled = world.CreateEntity();
        world.AddComponent(unscaled, new Transform2D(new Vector2(0, 10)));
        world.AddComponent(unscaled, Velocity2D.Zero);
        world.AddComponent(unscaled, new CharacterController2D(1, 1, gravityScale: 1f));

        var doubled = world.CreateEntity();
        world.AddComponent(doubled, new Transform2D(new Vector2(10, 10)));
        world.AddComponent(doubled, Velocity2D.Zero);
        world.AddComponent(doubled, new CharacterController2D(1, 1, gravityScale: 2f));

        var system = new CharacterControllerSystem(world, new Vector2(0, -10));

        system.Update(1f);

        var unscaledVelocity = world.GetComponent<Velocity2D>(unscaled).Linear.Y;
        var doubledVelocity = world.GetComponent<Velocity2D>(doubled).Linear.Y;

        Assert.Equal(-10f, unscaledVelocity, 0.001f);
        Assert.Equal(-20f, doubledVelocity, 0.001f);
    }

    [Fact]
    public void Constructor_DefaultGravity_ShouldBeEarthLike()
    {
        var world = new World();
        var system = new CharacterControllerSystem(world);

        Assert.Equal(new Vector2(0, -9.81f), system.Gravity);
    }
}
