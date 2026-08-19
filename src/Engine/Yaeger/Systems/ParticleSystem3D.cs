using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Simulates particles for every entity carrying a <see cref="ParticleEmitter3D"/> and a
/// <see cref="Transform3D"/>. Each emitter owns a fixed-size <see cref="ParticlePool3D"/> whose
/// particle storage and recycling never allocate after construction — mirrors the 2D
/// <see cref="ParticleSystem"/>'s simulation half exactly, but stays platform-agnostic (no
/// <see cref="Yaeger.Platform.IRenderSurface"/> dependency, since there is no 3D equivalent) so it
/// runs headless in <c>Yaeger.Core</c>. Rendering is a separate concern, living only in the native
/// <c>Yaeger</c> assembly: pair with <c>Yaeger.Systems.ParticleRenderSystem3D</c>, which reads the
/// pools this system produces via <see cref="TryGetPool"/> and draws them through
/// <c>Yaeger.Rendering.Renderer3D</c>.
///
/// Call <see cref="Update"/> once per frame from the update loop, before
/// <c>ParticleRenderSystem3D.Render</c> in the render callback.
/// </summary>
public class ParticleSystem3D : IUpdateSystem
{
    private readonly World _world;
    private readonly Random _random;
    private readonly Dictionary<Entity, ParticlePool3D> _pools = new();
    private readonly List<Entity> _expiredPools = [];

    /// <param name="world">The world queried for emitter entities.</param>
    /// <param name="seed">Optional seed for the emission spread randomness, for deterministic runs.</param>
    public ParticleSystem3D(World world, int? seed = null)
    {
        _world = world;
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    /// <summary>
    /// Advances all live particles, recycles expired ones, and emits new particles
    /// according to each emitter's <see cref="ParticleEmitter3D.EmitRate"/>.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (
            (Entity entity, ParticleEmitter3D emitter, Transform3D transform) in _world.Query<
                ParticleEmitter3D,
                Transform3D
            >()
        )
        {
            if (emitter.MaxParticles <= 0)
            {
                // No capacity means no new pool and no emission, but a pool created while
                // MaxParticles was positive must keep aging so its particles die out instead of
                // rendering frozen forever. Once drained, drop the pool so a disabled emitter
                // doesn't pin its backing array; re-enabling recreates it.
                if (_pools.TryGetValue(entity, out var orphanedPool))
                {
                    orphanedPool.Update(deltaTime);
                    if (orphanedPool.AliveCount == 0)
                        _pools.Remove(entity);
                }
                continue;
            }

            var pool = GetOrCreatePool(entity, emitter.MaxParticles);
            pool.Update(deltaTime);
            Emit(pool, in emitter, transform.Position, deltaTime);
        }

        RemoveExpiredPools();
    }

    /// <summary>
    /// Exposes the pool backing <paramref name="entity"/>'s emitter, for
    /// <c>ParticleRenderSystem3D</c> and tests. A pool exists only after the first
    /// <see cref="Update"/>.
    /// </summary>
    public bool TryGetPool(
        Entity entity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ParticlePool3D? pool
    ) => _pools.TryGetValue(entity, out pool);

    private ParticlePool3D GetOrCreatePool(Entity entity, int maxParticles)
    {
        // Recreate the pool when MaxParticles changes so the emitter component stays the single
        // source of truth for capacity. This drops all live particles and resets the emission
        // carry-over — capacity changes are treated as a restart of the effect, not a resize.
        if (_pools.TryGetValue(entity, out var pool) && pool.Capacity == maxParticles)
            return pool;

        pool = new ParticlePool3D(maxParticles);
        _pools[entity] = pool;
        return pool;
    }

    private void Emit(
        ParticlePool3D pool,
        in ParticleEmitter3D emitter,
        Vector3 origin,
        float deltaTime
    )
    {
        if (emitter.EmitRate <= 0f || emitter.ParticleLifetime <= 0f)
            return;

        pool.EmissionAccumulator += emitter.EmitRate * deltaTime;
        while (pool.EmissionAccumulator >= 1f)
        {
            pool.EmissionAccumulator -= 1f;
            if (!pool.TrySpawn(origin, RandomVelocity(in emitter), emitter.ParticleLifetime))
            {
                // Pool is saturated — drop the backlog instead of bursting on the next recycle.
                pool.EmissionAccumulator = 0f;
                break;
            }
        }
    }

    private Vector3 RandomVelocity(in ParticleEmitter3D emitter) =>
        RandomDirectionInCone(emitter.EmitDirection, emitter.SpreadAngle, _random)
        * emitter.InitialSpeed;

    /// <summary>
    /// Samples a unit direction deviating from <paramref name="axis"/> by a polar angle uniformly
    /// random in [0, <paramref name="spreadAngle"/> / 2] and an azimuthal angle uniformly random in
    /// [0, 2π) — a uniform-angle cone sample (not solid-angle-weighted), mirroring the simplicity of
    /// the 2D system's uniform-arc spread. Falls back to +Y when <paramref name="axis"/> is zero.
    /// Public (like <c>Yaeger.Rendering.TransparencySorter.ViewDepth</c>) so it's directly
    /// unit-testable without going through the emission-rate/accumulator machinery.
    /// </summary>
    public static Vector3 RandomDirectionInCone(Vector3 axis, float spreadAngle, Random random)
    {
        var normalizedAxis = axis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(axis);

        if (spreadAngle <= 0f)
            return normalizedAxis;

        // Any vector not parallel to the axis works as the seed for an orthonormal basis.
        var seed = MathF.Abs(normalizedAxis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(normalizedAxis, seed));
        var v = Vector3.Cross(normalizedAxis, u);

        var theta = (float)random.NextDouble() * (spreadAngle * 0.5f);
        var phi = (float)random.NextDouble() * MathF.Tau;

        return normalizedAxis * MathF.Cos(theta)
            + (u * MathF.Cos(phi) + v * MathF.Sin(phi)) * MathF.Sin(theta);
    }

    private void RemoveExpiredPools()
    {
        // An emitter is only simulated while it carries both components, so a pool whose entity
        // lost either one would otherwise be retained forever.
        var emitterStore = _world.GetStore<ParticleEmitter3D>();
        var transformStore = _world.GetStore<Transform3D>();
        foreach (var entity in _pools.Keys)
        {
            if (!emitterStore.TryGet(entity, out _) || !transformStore.TryGet(entity, out _))
                _expiredPools.Add(entity);
        }

        foreach (var entity in _expiredPools)
            _pools.Remove(entity);
        _expiredPools.Clear();
    }
}
