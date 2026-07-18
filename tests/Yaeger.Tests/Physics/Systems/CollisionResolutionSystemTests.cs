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
    public void Resolve_TriggerManifold_ShouldNotChangeVelocitiesOrPositions()
    {
        // Arrange — same overlapping setup as the non-trigger test below, but the manifold is
        // marked as a trigger. Resolution must be a complete no-op: no impulse, no friction, no
        // positional correction (the manifold itself, and OnCollision, are still reported
        // upstream by PhysicsWorld2D — this system only owns resolution).
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
            IsTrigger = true,
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — velocities and positions are untouched.
        Assert.Equal(new Vector2(5, 0), world.GetComponent<Velocity2D>(a).Linear);
        Assert.Equal(new Vector2(-5, 0), world.GetComponent<Velocity2D>(b).Linear);
        Assert.Equal(new Vector2(0, 0), world.GetComponent<Transform2D>(a).Position);
        Assert.Equal(new Vector2(1.5f, 0), world.GetComponent<Transform2D>(b).Position);
    }

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

    [Fact]
    public void Resolve_WithoutPhysicsMaterial_ShouldUseDefaultMaterial()
    {
        // Arrange — entities without PhysicsMaterial should use PhysicsMaterial.Default
        var world = new World();

        var a = world.CreateEntity();
        world.AddComponent(a, new Transform2D(new Vector2(0, 0)));
        world.AddComponent(a, new Velocity2D(10, 0));
        world.AddComponent(a, RigidBody2D.CreateDynamic(1.0f));
        // No PhysicsMaterial added — should fall back to Default (Restitution=0.3, Friction=0.4)

        var b = world.CreateEntity();
        world.AddComponent(b, new Transform2D(new Vector2(1, 0)));
        world.AddComponent(b, Velocity2D.Zero);
        world.AddComponent(b, RigidBody2D.CreateStatic());
        // No PhysicsMaterial added

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

        // Assert — with Default restitution of 0.3, the body should bounce back somewhat
        var velA = world.GetComponent<Velocity2D>(a);
        Assert.True(velA.Linear.X < 0); // Should have reversed due to restitution
    }

    // ── One-way platforms ─────────────────────────────────────────────────────

    [Fact]
    public void Resolve_OneWayPlatformIsEntityA_BodyApproachingFromBelow_ShouldNotResolve()
    {
        // Arrange — platform (one-way, default "up" surface) is EntityA; a dynamic body is
        // below it and jumping upward through it, so the manifold's normal (computed as if by
        // narrowphase) points down from the platform onto the body below.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(4, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 4.3f)));
        world.AddComponent(player, new Velocity2D(0, 8)); // jumping upward
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));

        var manifold = new CollisionManifold
        {
            EntityA = platform,
            EntityB = player,
            Normal = new Vector2(0, -1), // player is below the platform
            PenetrationDepth = 0.2f,
            ContactPoint = new Vector2(0, 4.5f),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — completely unresolved: still rising, unaffected by the platform.
        Assert.Equal(new Vector2(0, 8), world.GetComponent<Velocity2D>(player).Linear);
        Assert.Equal(new Vector2(0, 4.3f), world.GetComponent<Transform2D>(player).Position);
    }

    [Fact]
    public void Resolve_OneWayPlatformIsEntityB_BodyApproachingFromBelow_ShouldNotResolve()
    {
        // Arrange — same scenario as above, but the platform is EntityB this time, so the
        // manifold's normal is flipped (points up from the player to the platform above it).
        // The skip decision must be identical regardless of which side the platform is on.
        var world = new World();

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 4.3f)));
        world.AddComponent(player, new Velocity2D(0, 8));
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(4, 0.5f), oneWay: true));

        var manifold = new CollisionManifold
        {
            EntityA = player,
            EntityB = platform,
            Normal = new Vector2(0, 1), // platform is above the player
            PenetrationDepth = 0.2f,
            ContactPoint = new Vector2(0, 4.5f),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert
        Assert.Equal(new Vector2(0, 8), world.GetComponent<Velocity2D>(player).Linear);
        Assert.Equal(new Vector2(0, 4.3f), world.GetComponent<Transform2D>(player).Position);
    }

    [Fact]
    public void Resolve_OneWayPlatform_BodyLandingFromAbove_ShouldResolveNormally()
    {
        // Arrange — the body is above the platform and falling onto its top surface: a normal
        // landing, which must resolve exactly as a regular (non-one-way) collision would.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(4, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 5.7f)));
        world.AddComponent(player, new Velocity2D(0, -8)); // falling
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));
        world.AddComponent(player, PhysicsMaterial.Default);

        var manifold = new CollisionManifold
        {
            EntityA = platform,
            EntityB = player,
            Normal = new Vector2(0, 1), // player is above the platform
            PenetrationDepth = 0.05f,
            ContactPoint = new Vector2(0, 5.25f),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold]);

        // Assert — resolved: the fall is arrested/reversed, unlike the jump-through cases above.
        var velocity = world.GetComponent<Velocity2D>(player);
        Assert.True(velocity.Linear.Y > -8);
    }

    [Fact]
    public void Resolve_OneWayPlatform_DroppingThroughEntity_ShouldSkipEvenWhenLanding()
    {
        // Arrange — same "landing from above" setup that resolves normally above, but the
        // player is in the caller-supplied drop-through set this time.
        var world = new World();

        var platform = world.CreateEntity();
        world.AddComponent(platform, new Transform2D(new Vector2(0, 5)));
        world.AddComponent(platform, RigidBody2D.CreateStatic());
        world.AddComponent(platform, new BoxCollider2D(new Vector2(4, 0.5f), oneWay: true));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(new Vector2(0, 5.7f)));
        world.AddComponent(player, new Velocity2D(0, -8));
        world.AddComponent(player, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(player, new BoxCollider2D(1, 1));

        var manifold = new CollisionManifold
        {
            EntityA = platform,
            EntityB = player,
            Normal = new Vector2(0, 1),
            PenetrationDepth = 0.05f,
            ContactPoint = new Vector2(0, 5.25f),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifold], new HashSet<Entity> { player });

        // Assert — unresolved despite falling straight onto the top surface.
        Assert.Equal(new Vector2(0, -8), world.GetComponent<Velocity2D>(player).Linear);
        Assert.Equal(new Vector2(0, 5.7f), world.GetComponent<Transform2D>(player).Position);
    }

    [Fact]
    public void Resolve_OneWayPlatform_CustomSurfaceDirection_FiltersAlongThatAxis()
    {
        // Arrange — a one-way platform facing right (solid on its right side, pass-through
        // from the left), e.g. a wall you can jump through sideways but land against.
        var world = new World();
        var surfaceDirection = Vector2.UnitX;

        var wall = world.CreateEntity();
        world.AddComponent(wall, new Transform2D(new Vector2(5, 0)));
        world.AddComponent(wall, RigidBody2D.CreateStatic());
        world.AddComponent(
            wall,
            new BoxCollider2D(
                new Vector2(0.5f, 4),
                oneWay: true,
                surfaceDirection: surfaceDirection
            )
        );

        // Approaching from the left (the non-solid side): normal points from the wall back
        // towards the approaching body, i.e. in the -X direction.
        var fromLeft = world.CreateEntity();
        world.AddComponent(fromLeft, new Transform2D(new Vector2(4.3f, 0)));
        world.AddComponent(fromLeft, new Velocity2D(8, 0)); // moving right, into the wall
        world.AddComponent(fromLeft, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(fromLeft, new BoxCollider2D(1, 1));

        var manifoldFromLeft = new CollisionManifold
        {
            EntityA = wall,
            EntityB = fromLeft,
            Normal = new Vector2(-1, 0),
            PenetrationDepth = 0.2f,
            ContactPoint = new Vector2(4.75f, 0),
        };

        // Approaching from the right (the solid side): normal points away from the wall in +X.
        var fromRight = world.CreateEntity();
        world.AddComponent(fromRight, new Transform2D(new Vector2(5.7f, 0)));
        world.AddComponent(fromRight, new Velocity2D(-8, 0)); // moving left, into the wall
        world.AddComponent(fromRight, RigidBody2D.CreateDynamic(1.0f));
        world.AddComponent(fromRight, new BoxCollider2D(1, 1));
        world.AddComponent(fromRight, PhysicsMaterial.Default);

        var manifoldFromRight = new CollisionManifold
        {
            EntityA = wall,
            EntityB = fromRight,
            Normal = new Vector2(1, 0),
            PenetrationDepth = 0.05f,
            ContactPoint = new Vector2(5.25f, 0),
        };

        var system = new CollisionResolutionSystem(world);

        // Act
        system.Resolve([manifoldFromLeft, manifoldFromRight]);

        // Assert
        Assert.Equal(new Vector2(8, 0), world.GetComponent<Velocity2D>(fromLeft).Linear); // passed through
        var fromRightVelocity = world.GetComponent<Velocity2D>(fromRight);
        Assert.True(fromRightVelocity.Linear.X > -8); // resolved (arrested/reversed)
    }
}
