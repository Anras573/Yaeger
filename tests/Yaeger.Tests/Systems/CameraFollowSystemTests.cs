using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class CameraFollowSystemTests
{
    // ── End-to-end (window: null, so aspectRatio defaults to 1) ────────────────

    [Fact]
    public void Update_ZeroSmoothing_ShouldSnapDirectlyToTarget()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(10, 5)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 0f));

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        Assert.Equal(new Vector2(10, 5), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_WithSmoothing_ShouldMovePartwayTowardsTarget()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(10, 0)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 5f));

        var system = new CameraFollowSystem(world);

        system.Update(0.1f);

        var position = world.GetComponent<Camera2D>(cameraEntity).Position;
        Assert.True(position.X > 0f, "Expected some movement towards the target");
        Assert.True(position.X < 10f, "Expected not to have fully reached the target in one step");
    }

    [Fact]
    public void Update_TargetWithinDeadzone_ShouldNotMoveCamera()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(0.5f, 0.3f)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(
            cameraEntity,
            new CameraFollow(target, smoothing: 0f, deadzoneHalfExtents: new Vector2(1, 1))
        );

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        Assert.Equal(Vector2.Zero, world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_TargetExitsDeadzone_ShouldMoveByOverflowOnly()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(3, 0)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(
            cameraEntity,
            new CameraFollow(target, smoothing: 0f, deadzoneHalfExtents: new Vector2(1, 0))
        );

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        // delta.X = 3, deadzone half-extent 1 → overflow = 2.
        Assert.Equal(new Vector2(2, 0), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_LookAheadWithTargetVelocity_ShouldBiasTowardsMovementDirection()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(Vector2.Zero));
        world.AddComponent(target, new Velocity2D(4, 0));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(
            cameraEntity,
            new CameraFollow(target, smoothing: 0f, lookAheadTime: 0.5f)
        );

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        // target position (0,0) + velocity (4,0) * lookAheadTime 0.5 = (2, 0).
        Assert.Equal(new Vector2(2, 0), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_LookAheadWithoutTargetVelocity_ShouldHaveNoEffect()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(5, 0))); // no Velocity2D

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(
            cameraEntity,
            new CameraFollow(target, smoothing: 0f, lookAheadTime: 0.5f)
        );

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        Assert.Equal(new Vector2(5, 0), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_TargetDestroyed_ShouldHoldLastCameraPosition()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(10, 10)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(new Vector2(3, 4)));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 0f));

        var system = new CameraFollowSystem(world);

        world.DestroyEntity(target);
        system.Update(1f / 60f);

        Assert.Equal(new Vector2(3, 4), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_WithCameraBounds_ShouldClampVisibleSpanWithinBounds()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(0.3f, 0.3f))); // near the corner

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 0f));
        world.AddComponent(cameraEntity, new CameraBounds(Vector2.Zero, new Vector2(100, 50)));

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        // aspectRatio 1, zoom 1 → half-extents (1, 1); clamped away from the exact corner.
        Assert.Equal(new Vector2(1, 1), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    [Fact]
    public void Update_WithoutCameraBounds_ShouldNotClamp()
    {
        var world = new World();

        var target = world.CreateEntity();
        world.AddComponent(target, new Transform2D(new Vector2(0.3f, 0.3f)));

        var cameraEntity = world.CreateEntity();
        world.AddComponent(cameraEntity, new Camera2D(Vector2.Zero));
        world.AddComponent(cameraEntity, new CameraFollow(target, smoothing: 0f));

        var system = new CameraFollowSystem(world);

        system.Update(1f / 60f);

        Assert.Equal(new Vector2(0.3f, 0.3f), world.GetComponent<Camera2D>(cameraEntity).Position);
    }

    // ── Pure math helpers (direct, so aspect ratio/zoom can vary without a Window) ──

    [Fact]
    public void ApplyDeadzone_WithinBox_ShouldReturnCurrentPosition()
    {
        var result = CameraFollowSystem.ApplyDeadzone(
            currentPosition: Vector2.Zero,
            desiredPosition: new Vector2(0.5f, -0.5f),
            deadzoneHalfExtents: new Vector2(1, 1)
        );

        Assert.Equal(Vector2.Zero, result);
    }

    [Fact]
    public void ApplyDeadzone_OutsideBox_ShouldReturnPositionPulledToEdge()
    {
        var result = CameraFollowSystem.ApplyDeadzone(
            currentPosition: Vector2.Zero,
            desiredPosition: new Vector2(-3, 5),
            deadzoneHalfExtents: new Vector2(1, 2)
        );

        Assert.Equal(new Vector2(-2, 3), result);
    }

    [Fact]
    public void ApplySmoothing_NonPositiveSmoothing_ShouldSnapToTarget()
    {
        var result = CameraFollowSystem.ApplySmoothing(
            currentPosition: Vector2.Zero,
            targetPosition: new Vector2(10, 10),
            smoothing: 0f,
            deltaTime: 0.001f
        );

        Assert.Equal(new Vector2(10, 10), result);
    }

    [Fact]
    public void ApplySmoothing_PositiveSmoothing_ShouldMovePartway()
    {
        var result = CameraFollowSystem.ApplySmoothing(
            currentPosition: Vector2.Zero,
            targetPosition: new Vector2(10, 0),
            smoothing: 5f,
            deltaTime: 0.1f
        );

        Assert.True(result.X > 0f);
        Assert.True(result.X < 10f);
    }

    [Fact]
    public void ClampToBounds_LevelLargerThanViewport_ShouldClampToEdge()
    {
        var bounds = new CameraBounds(Vector2.Zero, new Vector2(100, 50));

        var result = CameraFollowSystem.ClampToBounds(
            position: new Vector2(0.3f, 0.3f),
            zoom: 1f,
            aspectRatio: 1f,
            bounds: bounds
        );

        Assert.Equal(new Vector2(1, 1), result);
    }

    [Fact]
    public void ClampToBounds_LevelNarrowerThanViewport_ShouldCenterOnThatAxis()
    {
        // Level is only 0.5 units wide, but at zoom 1 / aspect 1 the viewport needs 2 units.
        var bounds = new CameraBounds(Vector2.Zero, new Vector2(0.5f, 50));

        var result = CameraFollowSystem.ClampToBounds(
            position: new Vector2(0.4f, 0.4f),
            zoom: 1f,
            aspectRatio: 1f,
            bounds: bounds
        );

        Assert.Equal(0.25f, result.X, 0.0001f); // centered on the level's own midpoint
        Assert.Equal(1f, result.Y, 0.0001f); // Y still clamps normally
    }

    [Fact]
    public void ClampToBounds_HigherZoom_ShouldShrinkHalfExtentsAndAllowCloserToEdge()
    {
        var bounds = new CameraBounds(Vector2.Zero, new Vector2(100, 50));

        // At zoom 2, half-extents are (0.5, 0.5), so the camera can sit closer to the corner.
        var result = CameraFollowSystem.ClampToBounds(
            position: new Vector2(0.3f, 0.3f),
            zoom: 2f,
            aspectRatio: 1f,
            bounds: bounds
        );

        Assert.Equal(new Vector2(0.5f, 0.5f), result);
    }

    [Fact]
    public void ClampToBounds_WiderAspectRatio_ShouldWidenHorizontalHalfExtent()
    {
        var bounds = new CameraBounds(Vector2.Zero, new Vector2(100, 50));

        var result = CameraFollowSystem.ClampToBounds(
            position: new Vector2(0.3f, 0.3f),
            zoom: 1f,
            aspectRatio: 2f,
            bounds: bounds
        );

        Assert.Equal(new Vector2(2, 1), result);
    }
}
