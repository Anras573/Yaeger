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

    // ── Emission shapes and jitter ───────────────────────────────────────────

    [Fact]
    public void Update_WithPointShape_ShouldEmitAtExactEmitterPosition()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                Shape = EmissionShape.Point,
                DiscRadius = 5f, // ignored by Point
            },
            position: new Vector3(1f, 2f, 3f)
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        for (var i = 0; i < pool.AliveCount; i++)
            Assert.Equal(new Vector3(1f, 2f, 3f), pool[i].Position);
    }

    [Fact]
    public void Update_WithDiscShape_ShouldSpreadParticlesAcrossTheDisc()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                InitialSpeed = 0f,
                Shape = EmissionShape.Disc,
                DiscRadius = 2f,
                EmitDirection = Vector3.UnitY,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.True(pool.AliveCount > 1);

        // Not every particle spawns at the emitter's origin, and every offset stays within the
        // disc radius on the plane perpendicular to EmitDirection (Y stays ~0).
        var distinctPositions = false;
        for (var i = 0; i < pool.AliveCount; i++)
        {
            var position = pool[i].Position;
            if (position != Vector3.Zero)
                distinctPositions = true;

            Assert.Equal(0f, position.Y, 0.0001f);
            Assert.True(MathF.Sqrt(position.X * position.X + position.Z * position.Z) <= 2.0001f);
        }
        Assert.True(distinctPositions);
    }

    [Fact]
    public void Update_WithNonPositiveDiscRadius_ShouldBehaveLikePoint()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                Shape = EmissionShape.Disc,
                DiscRadius = 0f,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        for (var i = 0; i < pool.AliveCount; i++)
            Assert.Equal(Vector3.Zero, pool[i].Position);
    }

    [Fact]
    public void Update_WithSpeedVariance_ShouldVarySpeedAcrossParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                InitialSpeed = 2f,
                SpeedVariance = 0.5f,
                SpreadAngle = 0f,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        var speeds = new HashSet<float>();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            var speed = pool[i].Velocity.Length();
            Assert.InRange(speed, 1f, 3f); // 2 * (1 +/- 0.5)
            speeds.Add(speed);
        }
        Assert.True(speeds.Count > 1);
    }

    [Fact]
    public void Update_WithZeroSpeedVariance_ShouldMatchInitialSpeedExactly()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                InitialSpeed = 3f,
                SpeedVariance = 0f,
                SpreadAngle = 0f,
            }
        );

        system.Update(0.5f);

        Assert.True(system.TryGetPool(entity, out var pool));
        for (var i = 0; i < pool.AliveCount; i++)
            Assert.Equal(3f, pool[i].Velocity.Length(), 0.0001f);
    }

    [Fact]
    public void Update_WithLifetimeVariance_ShouldVaryLifetimeAcrossParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 4f,
                LifetimeVariance = 0.5f,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        var lifetimes = new HashSet<float>();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            Assert.InRange(pool[i].Lifetime, 2f, 6f); // 4 * (1 +/- 0.5)
            lifetimes.Add(pool[i].Lifetime);
        }
        Assert.True(lifetimes.Count > 1);
    }

    [Fact]
    public void Update_WithSizeVariance_ShouldVarySizeMultiplierAcrossParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                SizeVariance = 0.5f,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        var multipliers = new HashSet<float>();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            Assert.InRange(pool[i].SizeMultiplier, 0.5f, 1.5f);
            multipliers.Add(pool[i].SizeMultiplier);
        }
        Assert.True(multipliers.Count > 1);
    }

    [Fact]
    public void Update_WithZeroVariances_ShouldMatchExistingBehaviourExactly()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 2f,
                InitialSpeed = 1f,
                SpreadAngle = 0f,
            }
        );

        system.Update(0.5f);

        Assert.True(system.TryGetPool(entity, out var pool));
        for (var i = 0; i < pool.AliveCount; i++)
        {
            Assert.Equal(2f, pool[i].Lifetime, 0.0001f);
            Assert.Equal(1f, pool[i].SizeMultiplier, 0.0001f);
            Assert.Equal(0f, pool[i].InitialRotation);
        }
    }

    [Fact]
    public void Update_WithRandomInitialRotation_ShouldAssignVaryingRotations()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                RandomInitialRotation = true,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        var rotations = new HashSet<float>();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            Assert.InRange(pool[i].InitialRotation, 0f, MathF.Tau);
            rotations.Add(pool[i].InitialRotation);
        }
        Assert.True(rotations.Count > 1);
    }

    [Fact]
    public void Update_WithSeed_ShouldReproduceIdenticalOutputWithAllFeaturesInUse()
    {
        ParticlePool3D RunOnce()
        {
            var world = new World();
            var system = new ParticleSystem3D(world, seed: 1234);
            var entity = CreateEmitter(
                world,
                new ParticleEmitter3D(TexturePath)
                {
                    MaxParticles = 32,
                    EmitRate = 500f,
                    ParticleLifetime = 3f,
                    LifetimeVariance = 0.4f,
                    SpeedVariance = 0.4f,
                    SizeVariance = 0.4f,
                    Shape = EmissionShape.Disc,
                    DiscRadius = 1.5f,
                    RandomInitialRotation = true,
                }
            );

            system.Update(0.3f);
            system.Update(0.3f);

            Assert.True(system.TryGetPool(entity, out var pool));
            return pool;
        }

        var first = RunOnce();
        var second = RunOnce();

        Assert.Equal(first.AliveCount, second.AliveCount);
        for (var i = 0; i < first.AliveCount; i++)
        {
            Assert.Equal(first[i].Position, second[i].Position);
            Assert.Equal(first[i].Velocity, second[i].Velocity);
            Assert.Equal(first[i].Lifetime, second[i].Lifetime);
            Assert.Equal(first[i].SizeMultiplier, second[i].SizeMultiplier);
            Assert.Equal(first[i].InitialRotation, second[i].InitialRotation);
        }
    }

    // ── Flipbook frames ──────────────────────────────────────────────────────

    [Fact]
    public void Update_WithoutRandomStartFrame_ShouldSpawnEveryParticleOnFrameZero()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 32,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                FrameColumns = 4,
                FrameRows = 2,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.True(pool.AliveCount > 1);
        for (var i = 0; i < pool.AliveCount; i++)
            Assert.Equal(0, pool[i].StartFrame);
    }

    [Fact]
    public void Update_WithRandomStartFrame_ShouldVaryStartFrameAcrossParticles()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 64,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                FrameColumns = 4,
                FrameRows = 2,
                RandomStartFrame = true,
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        var startFrames = new HashSet<int>();
        for (var i = 0; i < pool.AliveCount; i++)
        {
            Assert.InRange(pool[i].StartFrame, 0, 7); // 4 columns * 2 rows
            startFrames.Add(pool[i].StartFrame);
        }
        Assert.True(startFrames.Count > 1);
    }

    [Fact]
    public void Update_WithRandomStartFrameOnSingleFrameGrid_ShouldAlwaysBeZero()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                MaxParticles = 32,
                EmitRate = 1000f,
                ParticleLifetime = 10f,
                RandomStartFrame = true, // FrameColumns/FrameRows left at their 1x1 default
            }
        );

        system.Update(1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        for (var i = 0; i < pool.AliveCount; i++)
            Assert.Equal(0, pool[i].StartFrame);
    }

    // ── Forces: acceleration, drag, turbulence ──────────────────────────────

    [Fact]
    public void Update_WithAcceleration_ShouldCurveParticleMotion()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                InitialSpeed = 1f,
                SpreadAngle = 0f,
                EmitDirection = Vector3.UnitX,
                Acceleration = new Vector3(0f, -9.8f, 0f), // gravity: a spark arcing back down
            }
        );

        // Forces integrate into a pool's *existing* particles at the top of each Update call, so
        // the particle spawned during the first call only picks up acceleration starting on the
        // second — same reason MeshRenderSystem-style per-frame systems need two ticks to observe
        // an effect that both writes and reads state in the same pass.
        system.Update(0.1f);
        system.Update(0.1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.True(pool.AliveCount >= 1);

        // A straight-line (unaccelerated) particle would keep Velocity.Y at 0; downward
        // acceleration must have pulled the first-spawned particle negative by now.
        Assert.True(pool[0].Velocity.Y < 0f);
    }

    [Fact]
    public void Update_WithDrag_ShouldSlowParticlesOverTime()
    {
        var world = new World();
        var system = new ParticleSystem3D(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter3D(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                InitialSpeed = 5f,
                SpreadAngle = 0f,
                Drag = 3f,
            }
        );

        // First call spawns the particle at full speed (no drag applied yet — see the note in
        // Update_WithAcceleration_ShouldCurveParticleMotion above).
        system.Update(0.1f);
        Assert.True(system.TryGetPool(entity, out var pool));

        system.Update(0.1f);
        var speedAfterOneDragStep = pool[0].Velocity.Length();

        system.Update(0.1f);
        var speedAfterTwoDragSteps = pool[0].Velocity.Length();

        Assert.True(speedAfterOneDragStep < 5f);
        Assert.True(speedAfterTwoDragSteps < speedAfterOneDragStep);
    }

    [Fact]
    public void Update_WithZeroAccelerationDragTurbulence_ShouldMatchConstantVelocityBehaviour()
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
        Assert.Equal(0f, pool[0].Velocity.X, 0.0001f);
        Assert.Equal(2f, pool[0].Velocity.Y, 0.0001f);
        Assert.Equal(0f, pool[0].Velocity.Z, 0.0001f);
    }

    [Fact]
    public void Update_WithSeed_ForceFeaturesReproduceIdenticalOutput()
    {
        ParticlePool3D RunOnce()
        {
            var world = new World();
            var system = new ParticleSystem3D(world, seed: 99);
            var entity = CreateEmitter(
                world,
                new ParticleEmitter3D(TexturePath)
                {
                    MaxParticles = 32,
                    EmitRate = 500f,
                    ParticleLifetime = 3f,
                    Acceleration = new Vector3(0f, 1.5f, 0f),
                    Drag = 0.5f,
                    Turbulence = 0.8f,
                    TurbulenceFrequency = 2f,
                }
            );

            system.Update(0.3f);
            system.Update(0.3f);

            Assert.True(system.TryGetPool(entity, out var pool));
            return pool;
        }

        var first = RunOnce();
        var second = RunOnce();

        Assert.Equal(first.AliveCount, second.AliveCount);
        for (var i = 0; i < first.AliveCount; i++)
        {
            Assert.Equal(first[i].Position, second[i].Position);
            Assert.Equal(first[i].Velocity, second[i].Velocity);
        }
    }

    // ── Jitter ────────────────────────────────────────────────────────────────

    [Fact]
    public void Jitter_WithZeroVariance_ReturnsBaseValueExactly()
    {
        var random = new Random(1);

        for (var i = 0; i < 20; i++)
            Assert.Equal(5f, ParticleSystem3D.Jitter(5f, 0f, random));
    }

    [Fact]
    public void Jitter_StaysWithinVarianceBounds()
    {
        var random = new Random(2);

        for (var i = 0; i < 200; i++)
        {
            var result = ParticleSystem3D.Jitter(10f, 0.3f, random);
            Assert.InRange(result, 7f, 13f);
        }
    }

    [Fact]
    public void Jitter_WithNegativeVariance_ClampsToZero()
    {
        var random = new Random(3);

        for (var i = 0; i < 20; i++)
            Assert.Equal(4f, ParticleSystem3D.Jitter(4f, -1f, random));
    }

    // ── SampleDiscOffset ─────────────────────────────────────────────────────

    [Fact]
    public void SampleDiscOffset_WithNonPositiveRadius_ReturnsZero()
    {
        var random = new Random(4);

        Assert.Equal(Vector3.Zero, ParticleSystem3D.SampleDiscOffset(Vector3.UnitY, 0f, random));
        Assert.Equal(Vector3.Zero, ParticleSystem3D.SampleDiscOffset(Vector3.UnitY, -1f, random));
    }

    [Fact]
    public void SampleDiscOffset_StaysWithinRadiusAndPerpendicularToAxis()
    {
        var random = new Random(5);
        var axis = Vector3.UnitY;

        for (var i = 0; i < 100; i++)
        {
            var offset = ParticleSystem3D.SampleDiscOffset(axis, 3f, random);

            Assert.True(offset.Length() <= 3.0001f);
            Assert.Equal(0f, Vector3.Dot(offset, axis), 0.0001f);
        }
    }

    [Fact]
    public void SampleDiscOffset_ZeroAxis_FallsBackToUnitY()
    {
        var random = new Random(6);

        var offset = ParticleSystem3D.SampleDiscOffset(Vector3.Zero, 2f, random);

        Assert.Equal(0f, Vector3.Dot(offset, Vector3.UnitY), 0.0001f);
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
