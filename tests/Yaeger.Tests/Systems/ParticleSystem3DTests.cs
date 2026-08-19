using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class ParticleSystem3DTests
{
    private const string TexturePath = "Assets/particle.png";

    private static Entity CreateEmitter(
        World world,
        ParticleEmitter3D emitter,
        Vector3? position = null
    )
    {
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Transform3D(position ?? Vector3.Zero, Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(entity, emitter);
        return entity;
    }

    [Fact]
    public void Update_ShouldEmitAccordingToEmitRate()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldCarryFractionalEmissionAcrossFrames()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath) { EmitRate = 1f, ParticleLifetime = 10f }
        );

        // 0.6 + 0.6 particles worth of emission — only the second frame crosses 1.0.
        system.Update(0.6f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(0, pool.AliveCount);

        system.Update(0.6f);
        Assert.Equal(1, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldNotExceedMaxParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 8,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(8, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldRecycleExpiredParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath) { EmitRate = 10f, ParticleLifetime = 1f }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);

        // Stop emission, then advance past every particle's lifetime.
        var emitter = world.GetComponent<ParticleEmitter3D>(entity);
        emitter.EmitRate = 0f;
        world.AddComponent(entity, emitter);

        system.Update(2f);

        Assert.Equal(0, pool.AliveCount);
    }

    [Fact]
    public void Update_WithZeroSpread_ShouldEmitAlongEmitDirection()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                EmitDirection = new Vector3(0f, 1f, 0f),
                SpreadAngle = 0f,
                InitialSpeed = 2f,
            }
        );

        system.Update(0.1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(0f, pool[0].Velocity.X, 0.0001f);
        Assert.Equal(2f, pool[0].Velocity.Y, 0.0001f);
        Assert.Equal(0f, pool[0].Velocity.Z, 0.0001f);
    }

    [Fact]
    public void Update_ShouldEmitFromEmitterTransformPosition()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                InitialSpeed = 0f,
            },
            position: new Vector3(3f, -2f, 1f)
        );

        system.Update(0.1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(3f, pool[0].Position.X, 0.0001f);
        Assert.Equal(-2f, pool[0].Position.Y, 0.0001f);
        Assert.Equal(1f, pool[0].Position.Z, 0.0001f);
    }

    [Fact]
    public void Update_WhenEmitterEntityDestroyed_ShouldRemovePool()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out _));

        world.DestroyEntity(entity);
        system.Update(0.1f);

        Assert.False(system.TryGetPool(entity, out _));
    }

    [Fact]
    public void Update_WhenMaxParticlesDropsToZero_ShouldStillAgeOutExistingParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 8,
                EmitRate = 10f,
                ParticleLifetime = 1f,
            }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);

        // Zero capacity stops emission, but the already-live particles must keep aging out
        // rather than rendering frozen forever.
        var emitter = world.GetComponent<ParticleEmitter3D>(entity);
        emitter.MaxParticles = 0;
        world.AddComponent(entity, emitter);

        system.Update(2f);

        Assert.Equal(0, pool.AliveCount);
        // Once drained, the disabled emitter's pool is released so its backing array isn't
        // pinned; re-enabling MaxParticles recreates it.
        Assert.False(system.TryGetPool(entity, out _));
    }

    [Fact]
    public void Update_WhenTransformRemoved_ShouldRemovePool()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out _));

        // The emitter component remains, but without a Transform3D the entity is no longer
        // simulated — its pool must not be retained forever.
        world.RemoveComponent<Transform3D>(entity);
        system.Update(0.1f);

        Assert.False(system.TryGetPool(entity, out _));
    }

    [Fact]
    public void Update_WithChangedMaxParticles_ShouldRecreatePoolWithNewCapacity()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 4,
                EmitRate = 10f,
                ParticleLifetime = 10f,
            }
        );

        system.Update(0.1f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(4, pool.Capacity);

        var emitter = world.GetComponent<ParticleEmitter3D>(entity);
        emitter.MaxParticles = 16;
        world.AddComponent(entity, emitter);

        system.Update(0.1f);
        Assert.True(system.TryGetPool(entity, out pool));
        Assert.Equal(16, pool.Capacity);
    }

    // ── RandomDirectionInCone ────────────────────────────────────────────────

    [Fact]
    public void RandomDirectionInCone_ReturnsUnitVector()
    {
        var random = new Random(1);

        for (var i = 0; i < 50; i++)
        {
            var direction = ParticleSystem3D.RandomDirectionInCone(
                Vector3.UnitY,
                MathF.PI / 3f,
                random
            );

            Assert.Equal(1f, direction.Length(), 0.0001f);
        }
    }

    [Fact]
    public void RandomDirectionInCone_ZeroSpread_ReturnsAxisExactly()
    {
        var random = new Random(1);

        var direction = ParticleSystem3D.RandomDirectionInCone(new Vector3(0f, 0f, 2f), 0f, random);

        Assert.Equal(Vector3.UnitZ, direction);
    }

    [Fact]
    public void RandomDirectionInCone_ZeroAxis_FallsBackToUnitY()
    {
        var random = new Random(1);

        var direction = ParticleSystem3D.RandomDirectionInCone(Vector3.Zero, 0f, random);

        Assert.Equal(Vector3.UnitY, direction);
    }

    [Fact]
    public void RandomDirectionInCone_StaysWithinHalfSpreadAngleOfAxis()
    {
        var random = new Random(7);
        var axis = Vector3.Normalize(new Vector3(1f, 1f, 0f));
        var spreadAngle = MathF.PI / 2f;

        for (var i = 0; i < 100; i++)
        {
            var direction = ParticleSystem3D.RandomDirectionInCone(axis, spreadAngle, random);
            var angleFromAxis = MathF.Acos(Math.Clamp(Vector3.Dot(axis, direction), -1f, 1f));

            Assert.True(angleFromAxis <= spreadAngle / 2f + 0.001f);
        }
    }
}
