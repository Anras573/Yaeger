using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class ParticleSystemTests
{
    private const string TexturePath = "Assets/particle.png";

    private sealed class FakeRenderSurface : IRenderSurface
    {
        public readonly List<(Matrix4x4 Transform, string TexturePath, Vector4 Color)> Quads = [];
        public int FlushCount;

        public void BeginFrame() { }

        public void EndFrame() { }

        public void FlushQueuedQuads() => FlushCount++;

        public void SetCamera(Matrix4x4 viewProjection) { }

        public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
            Quads.Add((transform, texturePath, color));

        public void SubmitQuad(
            Matrix4x4 transform,
            string texturePath,
            Vector2 uvMin,
            Vector2 uvMax,
            Vector4 color
        ) => Quads.Add((transform, texturePath, color));
    }

    private static Entity CreateEmitter(
        World world,
        ParticleEmitter emitter,
        Vector2? position = null
    )
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(position ?? Vector2.Zero));
        world.AddComponent(entity, emitter);
        return entity;
    }

    [Fact]
    public void Update_ShouldEmitAccordingToEmitRate()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);
    }

    [Fact]
    public void Update_ShouldCarryFractionalEmissionAcrossFrames()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 1f, ParticleLifetime = 10f }
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
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
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
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 1f }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);

        // Stop emission, then advance past every particle's lifetime.
        var emitter = world.GetComponent<ParticleEmitter>(entity);
        emitter.EmitRate = 0f;
        world.AddComponent(entity, emitter);

        system.Update(2f);

        Assert.Equal(0, pool.AliveCount);
    }

    [Fact]
    public void Update_WithZeroSpread_ShouldEmitAlongEmitDirection()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                EmitDirection = new Vector2(0f, 1f),
                SpreadAngle = 0f,
                InitialSpeed = 2f,
            }
        );

        system.Update(0.1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(1, pool.AliveCount);
        Assert.Equal(0f, pool[0].Velocity.X, 0.0001f);
        Assert.Equal(2f, pool[0].Velocity.Y, 0.0001f);
    }

    [Fact]
    public void Update_ShouldEmitFromEmitterTransformPosition()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
            {
                EmitRate = 10f,
                ParticleLifetime = 10f,
                InitialSpeed = 0f,
            },
            position: new Vector2(3f, -2f)
        );

        system.Update(0.1f);

        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(3f, pool[0].Position.X, 0.0001f);
        Assert.Equal(-2f, pool[0].Position.Y, 0.0001f);
    }

    [Fact]
    public void Update_WhenEmitterEntityDestroyed_ShouldRemovePool()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
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
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
            {
                MaxParticles = 8,
                EmitRate = 10f,
                ParticleLifetime = 1f,
            }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(5, pool.AliveCount);

        // Zero capacity stops emission, but the already-live particles must keep aging
        // out rather than rendering frozen forever.
        var emitter = world.GetComponent<ParticleEmitter>(entity);
        emitter.MaxParticles = 0;
        world.AddComponent(entity, emitter);

        system.Update(2f);

        Assert.Equal(0, pool.AliveCount);
    }

    [Fact]
    public void Update_WhenTransformRemoved_ShouldRemovePool()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);
        Assert.True(system.TryGetPool(entity, out _));

        // The emitter component remains, but without a Transform2D the entity is no
        // longer simulated — its pool must not be retained (or rendered) forever.
        world.RemoveComponent<Transform2D>(entity);
        system.Update(0.1f);

        Assert.False(system.TryGetPool(entity, out _));
    }

    [Fact]
    public void Render_ShouldSubmitOneQuadPerLiveParticleAndFlush()
    {
        var world = new World();
        var renderer = new FakeRenderSurface();
        var system = new ParticleSystem(world, renderer, seed: 42);
        CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);
        system.Render();

        Assert.Equal(5, renderer.Quads.Count);
        Assert.All(renderer.Quads, quad => Assert.Equal(TexturePath, quad.TexturePath));
        Assert.Equal(1, renderer.FlushCount);
    }

    [Fact]
    public void Render_ShouldInterpolateColorAndSizeOverLifetime()
    {
        var world = new World();
        var renderer = new FakeRenderSurface();
        var system = new ParticleSystem(world, renderer, seed: 42);
        CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
            {
                MaxParticles = 1,
                EmitRate = 10f,
                ParticleLifetime = 1f,
                InitialSpeed = 0f,
                StartColor = new Color(255, 255, 255, 255),
                EndColor = new Color(255, 255, 255, 0),
                StartSize = 1f,
                EndSize = 0f,
            }
        );

        // First update spawns the single particle at age 0; second ages it to t = 0.5.
        // The pool is already full, so the second update cannot emit a fresh particle.
        system.Update(0.1f);
        system.Update(0.5f);
        system.Render();

        var quad = Assert.Single(renderer.Quads);
        // Scale is baked into the model matrix: M11 == size for an axis-aligned quad.
        Assert.Equal(0.5f, quad.Transform.M11, 0.01f);
        Assert.Equal(0.5f, quad.Color.W, 0.01f);
        Assert.Equal(1f, quad.Color.X, 0.01f);
    }

    [Fact]
    public void Render_WithoutRenderer_ShouldNotThrow()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);
        system.Render();
    }

    [Fact]
    public void Render_WhenTransformRemovedAfterUpdate_ShouldNotSubmitQuads()
    {
        var world = new World();
        var renderer = new FakeRenderSurface();
        var system = new ParticleSystem(world, renderer, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath) { EmitRate = 10f, ParticleLifetime = 10f }
        );

        system.Update(0.5f);

        // Removing the transform between Update and Render must suppress rendering even
        // though the pool still holds live particles until the next Update expires it.
        world.RemoveComponent<Transform2D>(entity);
        system.Render();

        Assert.Empty(renderer.Quads);
    }

    [Fact]
    public void Update_WithChangedMaxParticles_ShouldRecreatePoolWithNewCapacity()
    {
        var world = new World();
        var system = new ParticleSystem(world, seed: 42);
        var entity = CreateEmitter(
            world,
            new ParticleEmitter(TexturePath)
            {
                MaxParticles = 4,
                EmitRate = 10f,
                ParticleLifetime = 10f,
            }
        );

        system.Update(0.1f);
        Assert.True(system.TryGetPool(entity, out var pool));
        Assert.Equal(4, pool.Capacity);

        var emitter = world.GetComponent<ParticleEmitter>(entity);
        emitter.MaxParticles = 16;
        world.AddComponent(entity, emitter);

        system.Update(0.1f);
        Assert.True(system.TryGetPool(entity, out pool));
        Assert.Equal(16, pool.Capacity);
    }
}
