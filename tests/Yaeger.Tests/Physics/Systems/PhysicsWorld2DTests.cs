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

        // Act
        physics.Update(0.001f); // Very small step to minimize movement before collision

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

        // Act
        physics.Update(0.001f);

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
}
