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
        var entities = new[] { firedEvents[0].EntityA, firedEvents[0].EntityB };
        Assert.Contains(a, entities);
        Assert.Contains(b, entities);
        Assert.NotEqual(firedEvents[0].EntityA, firedEvents[0].EntityB);
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

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidBroadphaseCellSize_ShouldThrow(float cellSize)
    {
        // Arrange
        var world = new World();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PhysicsWorld2D(world, broadphaseCellSize: cellSize)
        );
    }

    [Fact]
    public void Update_TilemapFloor_ShouldGenerateMergedColliderCoveringTheWholeSpan()
    {
        // Arrange — a 6-wide, 1-tall solid floor made of individual 1x1 tiles.
        var world = new World();
        var tileset = new Tileset("Assets/tiles.png", columns: 1, solidTileIndices: [0]);
        var tilemap = new Tilemap(tileset, width: 6, height: 1, tiles: [0, 0, 0, 0, 0, 0]);

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(Vector2.Zero));
        world.AddComponent(floor, tilemap);

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        // Act
        physics.Update(0f);

        // Assert — a single merged BoxCollider2D spans the whole floor, not six per-tile ones.
        var (_, box, transform) = world.Query<BoxCollider2D, Transform2D>().Single();
        Assert.Equal(new Vector2(6, 1), box.Size);
        Assert.Equal(new Vector2(3, 0.5f), transform.Position);
    }

    [Fact]
    public void Update_BoxSlidingAcrossTileSeam_ShouldNeverReceiveXAxisNormal()
    {
        // Arrange — a 6-wide, 1-tall solid floor. A box straddles the seam between tile
        // columns 2 and 3 (x = 3), the exact spot a naive per-tile collider setup would
        // produce a spurious X-axis collision normal (the "tile-seam snag").
        var world = new World();
        var tileset = new Tileset("Assets/tiles.png", columns: 1, solidTileIndices: [0]);
        var tilemap = new Tilemap(tileset, width: 6, height: 1, tiles: [0, 0, 0, 0, 0, 0]);

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(Vector2.Zero));
        world.AddComponent(floor, tilemap);

        // Box resting on top of the floor (top of floor is y = 1), straddling x = 3, and
        // overlapping slightly so a manifold is produced.
        var box = world.CreateEntity();
        world.AddComponent(box, new Transform2D(new Vector2(3, 1.4f)));
        world.AddComponent(box, new Velocity2D(5, 0));
        world.AddComponent(box, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(box, new BoxCollider2D(1, 1));
        world.AddComponent(box, PhysicsMaterial.Default);

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        // Act
        physics.Update(0f);

        // Assert — the only manifold's normal is purely vertical.
        Assert.Single(physics.Manifolds);
        var normal = physics.Manifolds[0].Normal;
        Assert.Equal(0f, normal.X);
        Assert.NotEqual(0f, normal.Y);
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

        // Act — one fixed timestep (the smallest step that reliably runs the physics
        // pipeline at the default 120 Hz), to minimize movement before collision.
        physics.Update(physics.FixedTimeStep);

        // Assert — ball should have bounced (velocity Y should be positive or less negative)
        var ballVel = world.GetComponent<Velocity2D>(ball);
        // The ball was going down at -10, after bouncing off a bouncy floor it should reverse
        Assert.True(ballVel.Linear.Y > -10);
    }

    [Fact]
    public void Update_DynamicBodyOverlappingTrigger_ShouldPassThroughUnimpededButFireOnCollision()
    {
        // Arrange — a coin-style trigger sitting where a falling ball will pass straight
        // through it (no gravity here — velocity alone drives the overlap).
        var world = new World();

        var coin = world.CreateEntity();
        world.AddComponent(coin, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(coin, RigidBody2D.CreateStatic());
        world.AddComponent(coin, new CircleCollider2D(0.5f, isTrigger: true));

        var ball = world.CreateEntity();
        world.AddComponent(ball, new Transform2D(new Vector2(0, 0.2f)));
        world.AddComponent(ball, new Velocity2D(0, -10));
        world.AddComponent(ball, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(ball, new CircleCollider2D(0.5f));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var firedEvents = new List<CollisionManifold>();
        physics.OnCollision += manifold => firedEvents.Add(manifold);

        // Act — one fixed timestep, the smallest step that reliably runs the physics pipeline
        // at the default 120 Hz.
        physics.Update(physics.FixedTimeStep);

        // Assert — the overlap is still reported...
        Assert.Single(firedEvents);
        Assert.True(firedEvents[0].IsTrigger);

        // ...but the ball's velocity is completely unaffected (no impulse, no positional
        // correction) — it passes through the trigger exactly as if it were not there.
        var ballVelocity = world.GetComponent<Velocity2D>(ball);
        Assert.Equal(new Vector2(0, -10), ballVelocity.Linear);
    }

    // ── Collision enter/exit/stay events ─────────────────────────────────────

    [Fact]
    public void Update_OverlappingFor10Steps_ThenSeparating_ShouldFireOneEnterTenStaysOneExit()
    {
        // Arrange — two static, non-moving boxes overlapping from the start.
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, RigidBody2D.CreateStatic());
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, RigidBody2D.CreateStatic());
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var enters = new List<CollisionManifold>();
        var stays = new List<CollisionManifold>();
        var exits = new List<(Entity, Entity)>();
        physics.OnCollisionEnter += m => enters.Add(m);
        physics.OnCollision += m => stays.Add(m);
        physics.OnCollisionExit += (ea, eb) => exits.Add((ea, eb));

        // Act — 10 steps while the pair remains overlapping (dt=0 so nothing moves).
        for (var i = 0; i < 10; i++)
            physics.Update(0f);

        // Assert — exactly one enter, ten stays, and zero exits so far.
        Assert.Single(enters);
        Assert.Equal(10, stays.Count);
        Assert.Empty(exits);

        // Act — separate the pair and step once more.
        var transformB = world.GetComponent<Transform2D>(b);
        transformB.Position = new Vector2(100, 100);
        world.AddComponent(b, transformB);
        physics.Update(0f);

        // Assert — exactly one exit fires, with no further enters or stays.
        Assert.Single(enters);
        Assert.Equal(10, stays.Count);
        var exit = Assert.Single(exits);
        var exitEntities = new[] { exit.Item1, exit.Item2 };
        Assert.Contains(a, exitEntities);
        Assert.Contains(b, exitEntities);
    }

    [Fact]
    public void Update_DestroyingOverlappingEntity_ShouldFireExitOnNextStep()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, RigidBody2D.CreateStatic());
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, RigidBody2D.CreateStatic());
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var exits = new List<(Entity, Entity)>();
        physics.OnCollisionExit += (ea, eb) => exits.Add((ea, eb));

        physics.Update(0f); // establish contact
        Assert.Empty(exits);

        // Act — destroy one of the two contacting entities, then step again.
        world.DestroyEntity(b);
        physics.Update(0f);

        // Assert — the destroyed entity's contact ends with an exit, not silently.
        var exit = Assert.Single(exits);
        var exitEntities = new[] { exit.Item1, exit.Item2 };
        Assert.Contains(a, exitEntities);
        Assert.Contains(b, exitEntities);
    }

    [Fact]
    public void Update_SeparateThenReoverlap_ShouldFireEnterAgain()
    {
        // Arrange
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, RigidBody2D.CreateStatic());
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, RigidBody2D.CreateStatic());
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var enters = new List<CollisionManifold>();
        var exits = new List<(Entity, Entity)>();
        physics.OnCollisionEnter += m => enters.Add(m);
        physics.OnCollisionExit += (ea, eb) => exits.Add((ea, eb));

        // Act — overlap, separate, then re-overlap.
        physics.Update(0f); // enter #1
        Assert.Single(enters);

        var transformB = world.GetComponent<Transform2D>(b);
        transformB.Position = new Vector2(100, 100);
        world.AddComponent(b, transformB);
        physics.Update(0f); // exit #1
        Assert.Single(exits);

        transformB.Position = new Vector2(1, 0);
        world.AddComponent(b, transformB);
        physics.Update(0f); // enter #2

        // Assert
        Assert.Equal(2, enters.Count);
        Assert.Single(exits);
    }

    [Fact]
    public void Update_TriggerPair_ShouldFireEnterAndExit()
    {
        // Arrange — a trigger pair (coin pickup) should still fire enter/exit like a normal pair.
        var world = new World();

        var sensor = world.CreateEntity();
        world.AddComponent(sensor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(sensor, RigidBody2D.CreateStatic());
        world.AddComponent(sensor, new CircleCollider2D(1f, isTrigger: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0.5f, 0)));
        world.AddComponent(player, RigidBody2D.CreateStatic());
        world.AddComponent(player, new CircleCollider2D(1f));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        var enters = new List<CollisionManifold>();
        var exits = new List<(Entity, Entity)>();
        physics.OnCollisionEnter += m => enters.Add(m);
        physics.OnCollisionExit += (ea, eb) => exits.Add((ea, eb));

        // Act
        physics.Update(0f);

        var transformPlayer = world.GetComponent<Transform2D>(player);
        transformPlayer.Position = new Vector2(100, 100);
        world.AddComponent(player, transformPlayer);
        physics.Update(0f);

        // Assert
        var enter = Assert.Single(enters);
        Assert.True(enter.IsTrigger);
        Assert.Single(exits);
    }

    // ── One-way platforms ─────────────────────────────────────────────────────

    [Fact]
    public void Update_OneWayPlatform_JumpingBodyPassesThroughThenLandsOnTheWayDown()
    {
        // Arrange — a wide, thin one-way platform (default "up" surface); a body below it
        // jumps with enough velocity to clear it, then must fall back and land on top.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(10, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(player, new Velocity2D(0, 12)); // strong jump
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));
        world.AddComponent(player, PhysicsMaterial.Sticky); // no bounce, for a clean landing

        var physics = new PhysicsWorld2D(world, new Vector2(0, -20));

        var maxHeightReached = float.NegativeInfinity;
        const float dt = 1f / 60f;
        const int steps = 240; // 4 simulated seconds — well past the full jump arc

        // Act
        for (var i = 0; i < steps; i++)
        {
            physics.Update(dt);
            var y = world.GetComponent<Transform2D>(player).Position.Y;
            if (y > maxHeightReached)
                maxHeightReached = y;
        }

        // Assert — passed cleanly through and above the platform (a solid platform this size
        // would have stopped it around y ≈ 1.25; reaching well past the platform's top proves
        // it jumped through rather than being blocked).
        Assert.True(maxHeightReached > 3.0f);

        // ...then fell back and landed to rest on top of the platform (top at y = 2.25, plus
        // the player's half-height of 0.5).
        var finalPosition = world.GetComponent<Transform2D>(player).Position;
        var finalVelocity = world.GetComponent<Velocity2D>(player);
        Assert.Equal(2.75f, finalPosition.Y, 0.1f);
        Assert.Equal(0f, finalVelocity.Linear.Y, 0.3f);
    }

    [Fact]
    public void Update_OneWayPlatform_WalkingOffEdgeAndFallingBackOn_ShouldBehaveNormally()
    {
        // Arrange — falling onto the platform while moving sideways (as if having walked off
        // an edge elsewhere and dropped back down) must resolve exactly like any other landing.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(10, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(-2, 4)));
        world.AddComponent(player, new Velocity2D(1, 0));
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));
        world.AddComponent(player, PhysicsMaterial.Sticky);

        var physics = new PhysicsWorld2D(world, new Vector2(0, -20));

        for (var i = 0; i < 120; i++) // 2 seconds
            physics.Update(1f / 60f);

        // Assert — settled on top of the platform.
        var finalPosition = world.GetComponent<Transform2D>(player).Position;
        Assert.Equal(2.75f, finalPosition.Y, 0.1f);
    }

    [Fact]
    public void DropThrough_ShouldLetEntityFallThroughOneWayPlatformOnDemand()
    {
        // Arrange — a body resting on a one-way platform normally cannot fall through it; the
        // drop-through API is the down+jump escape hatch that lets it do so on demand.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 2)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(10, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        // Resting right at the platform's top surface (slightly overlapping).
        world.AddComponent(player, new Transform2D(new Vector2(0, 2.7f)));
        world.AddComponent(player, Velocity2D.Zero);
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -20));

        // Act
        physics.DropThrough(player, 0.5f);
        for (var i = 0; i < 30; i++) // 0.5 simulated seconds
            physics.Update(1f / 60f);

        // Assert — fell well below the platform instead of resting on top of it.
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.True(position.Y < 2.0f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void DropThrough_InvalidDuration_ShouldThrow(float duration)
    {
        var world = new World();
        var entity = world.CreateEntity();
        var physics = new PhysicsWorld2D(world);

        Assert.Throws<ArgumentOutOfRangeException>(() => physics.DropThrough(entity, duration));
    }

    // ── Fixed-timestep accumulator ───────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldDefaultFixedTimeStepAndMaxSubSteps()
    {
        // Arrange & Act
        var world = new World();
        var physics = new PhysicsWorld2D(world);

        // Assert
        Assert.Equal(1f / 120f, physics.FixedTimeStep, 0.00001f);
        Assert.Equal(8, physics.MaxSubSteps);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidFixedTimeStep_ShouldThrow(float fixedTimeStep)
    {
        var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PhysicsWorld2D(world, fixedTimeStep: fixedTimeStep)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidMaxSubSteps_ShouldThrow(int maxSubSteps)
    {
        var world = new World();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PhysicsWorld2D(world, maxSubSteps: maxSubSteps)
        );
    }

    [Fact]
    public void Update_DeltaTimeBelowFixedTimeStep_ShouldNotStepYet()
    {
        // Arrange — fixedTimeStep = 0.1; feeding less than that shouldn't run any physics yet.
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, Velocity2D.Zero);
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -10), fixedTimeStep: 0.1f);

        // Act — two calls of 0.04s each accumulate to 0.08s, still under the 0.1s fixed step.
        physics.Update(0.04f);
        physics.Update(0.04f);

        // Assert — no step has run, so gravity hasn't been applied at all yet.
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(0f, velocity.Linear.Y);
    }

    [Fact]
    public void Update_AccumulatedTimeCrossingFixedTimeStep_ShouldRunExactlyOneStep()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, Velocity2D.Zero);
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -10), fixedTimeStep: 0.1f);

        // Act — 0.04 + 0.04 + 0.04 = 0.12, crossing the 0.1s fixed step exactly once, leaving
        // 0.02s carried over.
        physics.Update(0.04f);
        physics.Update(0.04f);
        physics.Update(0.04f);

        // Assert — exactly one fixed step's worth of gravity applied (-10 * 0.1 = -1.0), not
        // more, not less.
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-1.0f, velocity.Linear.Y, 0.0001f);

        // The leftover 0.02s shows up as a 0.2 interpolation fraction of the next step.
        Assert.Equal(0.2f, physics.InterpolationAlpha, 0.0001f);
    }

    [Fact]
    public void Update_MultipleFixedTimeStepsInOneCall_ShouldRunThatManySubSteps()
    {
        // Arrange
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, Velocity2D.Zero);
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var physics = new PhysicsWorld2D(world, new Vector2(0, -10), fixedTimeStep: 0.1f);

        // Act — a single 0.25s call spans two full 0.1s steps, leaving 0.05s over.
        physics.Update(0.25f);

        // Assert — two steps' worth of gravity (-10 * 0.1 * 2 = -2.0).
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-2.0f, velocity.Linear.Y, 0.0001f);
        Assert.Equal(0.5f, physics.InterpolationAlpha, 0.0001f);
    }

    [Fact]
    public void Update_HugeDeltaTime_ShouldClampToMaxSubSteps()
    {
        // Arrange — a huge delta (as if from a debugger pause or a massive hitch) should not
        // make the world try to fully catch up; it's capped at maxSubSteps steps and the rest
        // of the backlog is discarded, not carried forward.
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(Vector2.Zero));
        world.AddComponent(entity, Velocity2D.Zero);
        world.AddComponent(entity, RigidBody2D.CreateDynamic(1.0f));

        var physics = new PhysicsWorld2D(
            world,
            new Vector2(0, -10),
            fixedTimeStep: 0.125f,
            maxSubSteps: 4
        );

        // Act — 100 simulated seconds in one call; only 4 * 0.125 = 0.5s of it should run.
        physics.Update(100f);

        // Assert — exactly 4 steps' worth of gravity (-10 * 0.125 * 4 = -5.0), nowhere near the
        // ~-1000 a full 100-second catch-up would produce.
        var velocity = world.GetComponent<Velocity2D>(entity);
        Assert.Equal(-5.0f, velocity.Linear.Y, 0.0001f);
        Assert.Equal(0f, physics.InterpolationAlpha, 0.0001f);
    }

    [Fact]
    public void Update_SameScenarioAtDifferentFrameRates_ShouldProduceIdenticalResults()
    {
        // Arrange — free-falling body, simulated for exactly one second, once fed in 30 fps
        // frame deltas and once fed in 240 fps frame deltas. The default 120 Hz fixed step
        // divides evenly into both (4 sub-steps per call at 30 fps; one sub-step per two calls
        // at 240 fps), so both should advance the exact same 120 physics steps overall —
        // the whole point of fixed-timestep stepping.
        var worldA = new World();
        var entityA = worldA.CreateEntity();
        worldA.AddComponent(entityA, new Transform2D(new Vector2(0, 1000)));
        worldA.AddComponent(entityA, Velocity2D.Zero);
        worldA.AddComponent(entityA, RigidBody2D.CreateDynamic(1.0f));
        var physicsA = new PhysicsWorld2D(worldA, new Vector2(0, -10));

        var worldB = new World();
        var entityB = worldB.CreateEntity();
        worldB.AddComponent(entityB, new Transform2D(new Vector2(0, 1000)));
        worldB.AddComponent(entityB, Velocity2D.Zero);
        worldB.AddComponent(entityB, RigidBody2D.CreateDynamic(1.0f));
        var physicsB = new PhysicsWorld2D(worldB, new Vector2(0, -10));

        // Act
        for (var i = 0; i < 30; i++)
            physicsA.Update(1f / 30f);

        for (var i = 0; i < 240; i++)
            physicsB.Update(1f / 240f);

        // Assert
        var velocityA = worldA.GetComponent<Velocity2D>(entityA).Linear.Y;
        var velocityB = worldB.GetComponent<Velocity2D>(entityB).Linear.Y;
        Assert.Equal(velocityA, velocityB, 0.001f);

        var positionA = worldA.GetComponent<Transform2D>(entityA).Position.Y;
        var positionB = worldB.GetComponent<Transform2D>(entityB).Position.Y;
        Assert.Equal(positionA, positionB, 0.001f);
    }

    [Fact]
    public void Update_ZeroDeltaTime_ShouldRunOneImmediateStepBypassingAccumulator()
    {
        // Arrange — a zero delta must still run detection/resolution/events immediately (the
        // pattern many tests in this file rely on), not silently do nothing while it waits for
        // the accumulator to fill.
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, RigidBody2D.CreateStatic());
        world.AddComponent(a, new BoxCollider2D(2, 2));

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, RigidBody2D.CreateStatic());
        world.AddComponent(b, new BoxCollider2D(2, 2));

        var physics = new PhysicsWorld2D(world, Vector2.Zero);

        // Act
        physics.Update(0f);

        // Assert
        Assert.NotEmpty(physics.Manifolds);
        Assert.Equal(0f, physics.InterpolationAlpha);
    }

    // ── Tunneling prevention ──────────────────────────────────────────────────

    [Fact]
    public void Update_FastFallingBody_ShouldNotTunnelThroughThinFloor()
    {
        // Arrange — a one-unit-thick floor, and a body falling so fast that even a single fixed
        // sub-step (1/120s by default) would naively carry it clean through to below the floor:
        // at 2000 units/s, one sub-step covers ~16.7 units, far more than the floor's thickness.
        var world = new World();

        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(floor, RigidBody2D.CreateStatic());
        world.AddComponent(floor, new BoxCollider2D(20, 1));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 20)));
        world.AddComponent(player, new Velocity2D(0, -2000)); // extremely fast fall
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));
        world.AddComponent(player, PhysicsMaterial.Sticky);

        var physics = new PhysicsWorld2D(world, Vector2.Zero); // no gravity; velocity alone

        // Act — the fall itself only takes 0.01s; run well past that so it has time to land.
        for (var i = 0; i < 30; i++)
            physics.Update(1f / 60f);

        // Assert — landed on top of the floor (top at y=0.5, plus the player's half-height of
        // 0.5), not somewhere far below it, which is what a tunneled-through body would show.
        var position = world.GetComponent<Transform2D>(player).Position;
        Assert.Equal(1.0f, position.Y, 0.01f);
    }
}
