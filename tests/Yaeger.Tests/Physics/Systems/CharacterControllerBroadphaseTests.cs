using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;

namespace Yaeger.Tests.Physics.Systems;

/// <summary>
/// The controller's broadphase is an optimisation only: move-and-slide must give exactly the
/// results a scan of every collider would.
/// </summary>
public class CharacterControllerBroadphaseTests
{
    [Fact]
    public void Update_ShouldResolveObstacleReachedOnlyAfterALargeDepenetrationPush()
    {
        // Arrange — the controller starts embedded 1.5 units deep in a floor, far more than its
        // own half-size. Pushed out of the floor, it lands inside a ledge that was nowhere near
        // where the step began.
        var world = new World();
        var floor = world.CreateEntity();
        world.AddComponent(floor, new Transform2D(new Vector2(0f, -4f)));
        world.AddComponent(floor, new BoxCollider2D(new Vector2(20f, 10f)));
        var ledge = world.CreateEntity();
        world.AddComponent(ledge, new Transform2D(new Vector2(0f, 1.4f)));
        world.AddComponent(ledge, new BoxCollider2D(new Vector2(4f, 0.4f)));

        var player = world.CreateEntity();
        world.AddComponent(player, new Transform2D(Vector2.Zero));
        world.AddComponent(player, new CharacterController2D(new Vector2(1f, 1f)));
        world.AddComponent(player, new Velocity2D(0f, 0f));

        var system = new CharacterControllerSystem(world, Vector2.Zero);

        // Act
        system.Update(1f / 60f);

        // Assert — out of the floor (y = 1.5), then out of the ledge on top of it (y = 2.1).
        var position = world.GetComponent<Transform2D>(player).Position;
        var controller = world.GetComponent<CharacterController2D>(player);
        Assert.Equal(2.1f, position.Y, 4);
        Assert.Equal(ledge, controller.GroundEntity);
    }

    [Fact]
    public void Update_ShouldMatchABruteForceScan_AcrossRandomScenes()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var indexed = Simulate(seed, scanAllColliders: false);
            var bruteForce = Simulate(seed, scanAllColliders: true);

            Assert.True(indexed == bruteForce, $"seed {seed}: {indexed} != {bruteForce}");
        }
    }

    // Runs one random scene (static obstacles, kinematic platforms, fast dynamic boxes and three
    // controllers, some spawned inside geometry) and returns a digest of every controller's
    // trajectory, contact flags, ground entity and contact events.
    private static string Simulate(int seed, bool scanAllColliders)
    {
        var rng = new Random(seed);
        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        var world = new World();
        var physics = new PhysicsWorld2D(world);
        var controllers = new CharacterControllerSystem(world)
        {
            ScanAllColliders = scanAllColliders,
        };

        var digest = new HashCode();
        controllers.OnContact += contact =>
        {
            digest.Add(contact.Controller.Id);
            digest.Add(contact.Other.Id);
            digest.Add(contact.Normal);
        };
        controllers.OnTriggerEnter += (controller, trigger) => digest.Add(trigger.Id);
        controllers.OnTriggerExit += (controller, trigger) => digest.Add(-trigger.Id);

        var obstacles = new List<Entity>();
        for (var i = 0; i < 40; i++)
        {
            var entity = world.CreateEntity();
            var big = rng.Next(6) == 0;
            world.AddComponent(
                entity,
                new Transform2D(new Vector2(Range(-20f, 20f), Range(-10f, 10f)))
            );
            world.AddComponent(
                entity,
                new BoxCollider2D(
                    new Vector2(Range(0.3f, big ? 25f : 4f), Range(0.3f, big ? 6f : 2f)),
                    isTrigger: rng.Next(6) == 0,
                    oneWay: rng.Next(6) == 0
                )
            );
            if (rng.Next(4) != 0)
                world.AddComponent(entity, RigidBody2D.CreateStatic());
            obstacles.Add(entity);
        }

        var platforms = new List<Entity>();
        for (var i = 0; i < 6; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(
                entity,
                new Transform2D(new Vector2(Range(-15f, 15f), Range(-8f, 8f)))
            );
            world.AddComponent(
                entity,
                new BoxCollider2D(new Vector2(Range(1f, 5f), Range(0.3f, 1f)))
            );
            world.AddComponent(entity, RigidBody2D.CreateKinematic());
            world.AddComponent(entity, new Velocity2D(Range(-3f, 3f), Range(-3f, 3f)));
            platforms.Add(entity);
        }

        for (var i = 0; i < 20; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(
                entity,
                new Transform2D(new Vector2(Range(-18f, 18f), Range(-5f, 14f)))
            );
            world.AddComponent(
                entity,
                new BoxCollider2D(new Vector2(Range(0.2f, 1.5f), Range(0.2f, 1.5f)))
            );
            world.AddComponent(entity, RigidBody2D.CreateDynamic(Range(0.5f, 3f)));
            var speed = rng.Next(4) == 0 ? 60f : 4f;
            world.AddComponent(entity, new Velocity2D(Range(-speed, speed), Range(-speed, speed)));
        }

        var players = new List<Entity>();
        for (var i = 0; i < 3; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent(
                entity,
                new Transform2D(new Vector2(Range(-18f, 18f), Range(-9f, 12f)))
            );
            world.AddComponent(
                entity,
                new CharacterController2D(
                    new Vector2(Range(0.4f, 1f), Range(0.6f, 1.6f)),
                    stepHeight: rng.Next(2) == 0 ? 0.3f : 0f
                )
            );
            world.AddComponent(entity, new Velocity2D(0f, 0f));
            players.Add(entity);
        }

        for (var step = 0; step < 120; step++)
        {
            foreach (var player in players)
            {
                var velocity = world.GetComponent<Velocity2D>(player);
                if (rng.Next(20) == 0)
                    velocity.Linear.X = Range(-8f, 8f);
                if (rng.Next(40) == 0)
                    velocity.Linear.Y = Range(2f, 12f);
                world.AddComponent(player, velocity);
            }

            if (step % 60 == 30)
            {
                foreach (var platform in platforms)
                {
                    var velocity = world.GetComponent<Velocity2D>(platform);
                    world.AddComponent(platform, new Velocity2D(-velocity.Linear));
                }

                world.DestroyEntity(obstacles[rng.Next(obstacles.Count)]);
                var moved = obstacles[rng.Next(obstacles.Count)];
                if (world.TryGetComponent<Transform2D>(moved, out var transform))
                {
                    transform.Position += new Vector2(Range(-2f, 2f), Range(-2f, 2f));
                    world.AddComponent(moved, transform);
                }
            }

            physics.Update(1f / 60f);
            controllers.Update(1f / 60f);

            foreach (var player in players)
            {
                var controller = world.GetComponent<CharacterController2D>(player);
                digest.Add(world.GetComponent<Transform2D>(player).Position);
                digest.Add(controller.IsGrounded);
                digest.Add(controller.IsTouchingWallLeft);
                digest.Add(controller.IsTouchingWallRight);
                digest.Add(controller.IsTouchingCeiling);
                digest.Add(controller.GroundEntity?.Id ?? -1);
            }
        }

        return digest.ToHashCode().ToString("x8");
    }
}
