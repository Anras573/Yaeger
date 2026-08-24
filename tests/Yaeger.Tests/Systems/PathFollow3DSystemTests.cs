using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class PathFollow3DSystemTests
{
    // ── Constant speed / leg boundaries ─────────────────────────────────────

    [Fact]
    public void Update_HeadingTowardsWaypoint_ShouldMoveAtConfiguredSpeed()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D([new Vector3(0, 0, 0), new Vector3(10, 0, 0)], speed: 2f)
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f);

        var transform = world.GetComponent<Transform3D>(entity);
        Assert.Equal(new Vector3(2, 0, 0), transform.Position);

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(2f, path.CurrentSpeed);
    }

    [Fact]
    public void Update_DifferingLegLengths_ShouldCoverEachAtTheSameSpeed()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 100)],
                speed: 1f,
                turnRate: 0f
            )
        );

        var system = new PathFollow3DSystem(world);

        // Short leg (length 1) should be crossed in exactly one second, regardless of the much
        // longer leg following it.
        system.Update(1f);
        var afterShortLeg = world.GetComponent<Transform3D>(entity).Position;
        Assert.Equal(new Vector3(1, 0, 0), afterShortLeg, EqualityComparer());

        var index = world.GetComponent<PathFollow3D>(entity).CurrentWaypointIndex;
        Assert.Equal(2, index);
    }

    [Fact]
    public void Update_StepLongerThanWholeLeg_ShouldCrossBoundaryWithoutStallingOrOvershooting()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 10)],
                speed: 5f,
                turnRate: 0f
            )
        );

        var system = new PathFollow3DSystem(world);

        // Budget for this step: 5 units. First leg is 2 units, leaving 3 units to spend on the
        // second leg (which is 10 units long) — so it should land at (2, 0, 3), not stall at the
        // first waypoint and not overshoot past it.
        system.Update(1f);

        var position = world.GetComponent<Transform3D>(entity).Position;
        Assert.Equal(new Vector3(2, 0, 3), position, EqualityComparer());

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(2, path.CurrentWaypointIndex);
        Assert.Equal(5f, path.CurrentSpeed);
    }

    // ── Loop mode boundaries ─────────────────────────────────────────────────

    [Fact]
    public void Update_OnceMode_ShouldHoldAtLastWaypointAndFinish()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(1, 0, 0)],
                speed: 10f,
                loopMode: TweenLoopMode.Once
            )
        );

        var system = new PathFollow3DSystem(world);

        // Way more time than needed to reach the (short) path's end.
        system.Update(10f);

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.True(path.IsFinished);
        Assert.Equal(0f, path.CurrentSpeed);

        var position = world.GetComponent<Transform3D>(entity).Position;
        Assert.Equal(new Vector3(1, 0, 0), position, EqualityComparer());

        // Further updates should hold in place rather than throwing or drifting.
        system.Update(1f);
        Assert.Equal(new Vector3(1, 0, 0), world.GetComponent<Transform3D>(entity).Position);
    }

    [Fact]
    public void Update_LoopMode_ShouldWrapBackToFirstWaypointAfterLast()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(10, 0, 0), Quaternion.Identity, Vector3.One)
        );
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0)],
            speed: 1f,
            loopMode: TweenLoopMode.Loop
        )
        {
            CurrentWaypointIndex = 1,
        };
        world.AddComponent(entity, path);

        var system = new PathFollow3DSystem(world);
        // At the last waypoint already; a full step should wrap towards the first and travel
        // the full step distance back towards it.
        system.Update(1f);

        var updated = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(0, updated.CurrentWaypointIndex);
        Assert.False(updated.IsFinished);

        var position = world.GetComponent<Transform3D>(entity).Position;
        Assert.Equal(new Vector3(9, 0, 0), position, EqualityComparer());
    }

    [Fact]
    public void Update_PingPongArrivingAtLastWaypoint_ShouldReverseDirection()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(10, 0, 0), Quaternion.Identity, Vector3.One)
        );
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0)],
            speed: 1f,
            loopMode: TweenLoopMode.PingPong
        )
        {
            CurrentWaypointIndex = 1,
            MovingForward = true,
        };
        world.AddComponent(entity, path);

        var system = new PathFollow3DSystem(world);
        system.Update(1f);

        var updated = world.GetComponent<PathFollow3D>(entity);
        Assert.False(updated.MovingForward);
        Assert.Equal(0, updated.CurrentWaypointIndex);

        var position = world.GetComponent<Transform3D>(entity).Position;
        Assert.Equal(new Vector3(9, 0, 0), position, EqualityComparer());
    }

    [Fact]
    public void Update_PingPongArrivingAtFirstWaypoint_ShouldReverseDirection()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        var path = new PathFollow3D(
            [new Vector3(0, 0, 0), new Vector3(10, 0, 0)],
            speed: 1f,
            loopMode: TweenLoopMode.PingPong
        )
        {
            CurrentWaypointIndex = 0,
            MovingForward = false,
        };
        world.AddComponent(entity, path);

        var system = new PathFollow3DSystem(world);
        system.Update(1f);

        var updated = world.GetComponent<PathFollow3D>(entity);
        Assert.True(updated.MovingForward);
        Assert.Equal(1, updated.CurrentWaypointIndex);
    }

    // ── Degenerate paths ─────────────────────────────────────────────────────

    [Fact]
    public void Update_ZeroWaypoints_ShouldNotThrowMoveOrProduceNaN()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var start = new Transform3D(new Vector3(1, 2, 3), Quaternion.Identity, Vector3.One);
        world.AddComponent(entity, start);
        world.AddComponent(entity, new PathFollow3D([], speed: 1f));

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var transform = world.GetComponent<Transform3D>(entity);
        Assert.Equal(start.Position, transform.Position);
        AssertFinite(transform);

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(0f, path.CurrentSpeed);
    }

    [Fact]
    public void Update_OneWaypoint_ShouldNotThrowMoveOrProduceNaN()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var start = new Transform3D(new Vector3(1, 2, 3), Quaternion.Identity, Vector3.One);
        world.AddComponent(entity, start);
        world.AddComponent(entity, new PathFollow3D([new Vector3(5, 5, 5)], speed: 1f));

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var transform = world.GetComponent<Transform3D>(entity);
        Assert.Equal(start.Position, transform.Position);
        AssertFinite(transform);

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(0f, path.CurrentSpeed);
    }

    [Fact]
    public void Update_DuplicateWaypoints_ShouldSkipZeroLengthLegsWithoutHangingOrThrowing()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(3, 0, 0),
                ],
                speed: 3f,
                turnRate: 0f
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f);

        var transform = world.GetComponent<Transform3D>(entity);
        AssertFinite(transform);
        // Budget of 3: 1 unit to the duplicate cluster (which costs nothing to pass through),
        // then 2 more to (3, 0, 0) — landing exactly there with no budget left to loop back with.
        Assert.Equal(new Vector3(3, 0, 0), transform.Position, EqualityComparer());
    }

    [Fact]
    public void Update_AllCoincidentWaypoints_ShouldNotHangAndShouldReportZeroSpeed()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var start = Transform3D.Identity;
        world.AddComponent(entity, start);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0)],
                speed: 1f,
                loopMode: TweenLoopMode.Loop
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var transform = world.GetComponent<Transform3D>(entity);
        AssertFinite(transform);
        Assert.Equal(start.Position, transform.Position);

        var path = world.GetComponent<PathFollow3D>(entity);
        Assert.Equal(0f, path.CurrentSpeed);
    }

    [Fact]
    public void Update_NonPositiveDeltaTime_ShouldBeNoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var start = new Transform3D(new Vector3(1, 2, 3), Quaternion.Identity, Vector3.One);
        world.AddComponent(entity, start);
        world.AddComponent(
            entity,
            new PathFollow3D([new Vector3(0, 0, 0), new Vector3(10, 0, 0)], speed: 1f)
        );

        var system = new PathFollow3DSystem(world);
        system.Update(0f);
        system.Update(-1f);

        var transform = world.GetComponent<Transform3D>(entity);
        Assert.Equal(start.Position, transform.Position);
    }

    // ── Facing ────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithZeroTurnRate_ShouldNeverRotate()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D([new Vector3(0, 0, 0), new Vector3(0, 0, 10)], speed: 1f, turnRate: 0f)
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        Assert.Equal(Quaternion.Identity, rotation);
    }

    [Fact]
    public void Update_WithModerateTurnRate_ShouldTurnGraduallyNotSnap()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        // Identity's forward is local -Z, i.e. world (0, 0, -1); travelling along +X is a 90°
        // turn away from it.
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(100, 0, 0)],
                speed: 1f,
                turnRate: MathF.PI / 2f // 90 degrees per second
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        Assert.NotEqual(Quaternion.Identity, rotation);

        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        // A small step at a slow turn rate should not have reached the full target facing yet.
        Assert.True(
            forward.X < 1f - 1e-3f,
            $"Expected a partial turn, not an instant snap. Forward: {forward}"
        );
    }

    [Fact]
    public void Update_WithLargeTurnRate_ShouldFaceDirectionOfTravelWithinOneStep()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(100, 0, 0)],
                speed: 1f,
                turnRate: 1000f
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Assert.Equal(1f, forward.X, 3);
        Assert.Equal(0f, forward.Y, 3);
        Assert.Equal(0f, forward.Z, 3);
    }

    [Fact]
    public void Update_YawOnly_ShouldIgnoreVerticalComponentOfTravel()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(10, 10, 0)],
                speed: 1f,
                turnRate: 1000f,
                yawOnly: true
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Assert.Equal(0f, forward.Y, 3);
    }

    [Fact]
    public void Update_FullFacing_ShouldTiltTowardsVerticalComponentOfTravel()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(10, 10, 0)],
                speed: 1f,
                turnRate: 1000f,
                yawOnly: false
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Assert.True(forward.Y > 0.1f, $"Expected an upward-tilted forward, got {forward}");
    }

    [Fact]
    public void Update_YawOnlyMovingParallelToUpAxis_ShouldHoldFacing()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity);
        world.AddComponent(
            entity,
            new PathFollow3D(
                [new Vector3(0, 0, 0), new Vector3(0, 10, 0)],
                speed: 1f,
                turnRate: 1000f,
                yawOnly: true
            )
        );

        var system = new PathFollow3DSystem(world);
        system.Update(1f / 60f);

        var rotation = world.GetComponent<Transform3D>(entity).Rotation;
        Assert.Equal(Quaternion.Identity, rotation);
    }

    // ── Pure math helpers ─────────────────────────────────────────────────

    [Fact]
    public void RotateTowards_NonPositiveMaxDelta_ShouldReturnCurrentUnchanged()
    {
        var current = Quaternion.Identity;
        var target = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

        var result = PathFollow3DSystem.RotateTowards(current, target, 0f);

        Assert.Equal(current, result);
    }

    [Fact]
    public void RotateTowards_LargeMaxDelta_ShouldReachTargetExactly()
    {
        var current = Quaternion.Identity;
        var target = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);

        var result = PathFollow3DSystem.RotateTowards(current, target, MathF.PI * 10f);

        Assert.Equal(target.X, result.X, 4);
        Assert.Equal(target.Y, result.Y, 4);
        Assert.Equal(target.Z, result.Z, 4);
        Assert.Equal(target.W, result.W, 4);
    }

    [Fact]
    public void ComputeFacing_ZeroDirection_ShouldReturnNull()
    {
        var result = PathFollow3DSystem.ComputeFacing(Vector3.Zero, Vector3.UnitY, yawOnly: false);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeFacing_NonFiniteDirection_ShouldReturnNull()
    {
        var result = PathFollow3DSystem.ComputeFacing(
            new Vector3(float.NaN, 0, 0),
            Vector3.UnitY,
            yawOnly: false
        );

        Assert.Null(result);
    }

    [Fact]
    public void LookRotation_ForwardParallelToUp_ShouldNotThrowOrProduceNaN()
    {
        var rotation = PathFollow3DSystem.LookRotation(Vector3.UnitY, Vector3.UnitY);

        Assert.True(float.IsFinite(rotation.X));
        Assert.True(float.IsFinite(rotation.Y));
        Assert.True(float.IsFinite(rotation.Z));
        Assert.True(float.IsFinite(rotation.W));
    }

    private static void AssertFinite(Transform3D transform)
    {
        Assert.True(float.IsFinite(transform.Position.X));
        Assert.True(float.IsFinite(transform.Position.Y));
        Assert.True(float.IsFinite(transform.Position.Z));
        Assert.True(float.IsFinite(transform.Rotation.X));
        Assert.True(float.IsFinite(transform.Rotation.Y));
        Assert.True(float.IsFinite(transform.Rotation.Z));
        Assert.True(float.IsFinite(transform.Rotation.W));
    }

    private static Vector3EqualityComparer EqualityComparer() => new();

    private sealed class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) =>
            MathF.Abs(a.X - b.X) < 0.001f
            && MathF.Abs(a.Y - b.Y) < 0.001f
            && MathF.Abs(a.Z - b.Z) < 0.001f;

        public int GetHashCode(Vector3 obj) => obj.GetHashCode();
    }
}
