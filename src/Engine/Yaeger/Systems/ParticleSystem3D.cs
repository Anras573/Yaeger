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
                    orphanedPool.Update(
                        deltaTime,
                        emitter.Acceleration,
                        emitter.Drag,
                        emitter.Turbulence,
                        emitter.TurbulenceFrequency
                    );
                    if (orphanedPool.AliveCount == 0)
                        _pools.Remove(entity);
                }
                continue;
            }

            var pool = GetOrCreatePool(entity, emitter.MaxParticles);
            pool.Update(
                deltaTime,
                emitter.Acceleration,
                emitter.Drag,
                emitter.Turbulence,
                emitter.TurbulenceFrequency
            );
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

            var position = origin;
            if (emitter.Shape == EmissionShape.Disc)
                position += SampleDiscOffset(emitter.EmitDirection, emitter.DiscRadius, _random);

            // Lifetime jitter is clamped away from zero: a jittered lifetime of 0 (or negative)
            // would spawn a particle already expired, silently dropping it from the plume instead
            // of visibly varying it.
            var lifetime = MathF.Max(
                Jitter(emitter.ParticleLifetime, emitter.LifetimeVariance, _random),
                1e-4f
            );
            var sizeMultiplier = MathF.Max(Jitter(1f, emitter.SizeVariance, _random), 0f);
            var initialRotation = emitter.RandomInitialRotation
                ? (float)_random.NextDouble() * MathF.Tau
                : 0f;

            if (
                !pool.TrySpawn(
                    position,
                    RandomVelocity(in emitter),
                    lifetime,
                    sizeMultiplier,
                    initialRotation
                )
            )
            {
                // Pool is saturated — drop the backlog instead of bursting on the next recycle.
                pool.EmissionAccumulator = 0f;
                break;
            }
        }
    }

    private Vector3 RandomVelocity(in ParticleEmitter3D emitter)
    {
        var speed = MathF.Max(Jitter(emitter.InitialSpeed, emitter.SpeedVariance, _random), 0f);
        return RandomDirectionInCone(emitter.EmitDirection, emitter.SpreadAngle, _random) * speed;
    }

    /// <summary>
    /// Applies fractional jitter to <paramref name="baseValue"/>: the result is
    /// <c>baseValue * (1 + U(-variance, variance))</c>, where <c>U</c> is a uniform sample drawn
    /// from <paramref name="random"/>. A <paramref name="variance"/> of 0 returns
    /// <paramref name="baseValue"/> exactly (no randomness consumed in that direction — the
    /// multiplier is exactly 1), which is what keeps every existing emitter's output unchanged.
    /// Public (like <see cref="RandomDirectionInCone"/>) so it's directly unit-testable.
    /// </summary>
    public static float Jitter(float baseValue, float variance, Random random)
    {
        var clampedVariance = MathF.Max(variance, 0f);
        var offset = (float)(random.NextDouble() * 2.0 - 1.0) * clampedVariance;
        return baseValue * (1f + offset);
    }

    /// <summary>
    /// Samples a random point uniformly across a disc of <paramref name="radius"/>, centred on the
    /// origin and oriented perpendicular to <paramref name="axis"/> — the offset a
    /// <see cref="EmissionShape.Disc"/> emitter adds to its spawn position. Uses
    /// <c>sqrt</c>-scaled radius sampling so points are uniform by area rather than biased toward
    /// the centre. Returns <see cref="Vector3.Zero"/> for a non-positive radius. Falls back to +Y
    /// when <paramref name="axis"/> is zero, mirroring <see cref="RandomDirectionInCone"/>. Public
    /// so it's directly unit-testable without going through the emission-rate/accumulator machinery.
    /// </summary>
    public static Vector3 SampleDiscOffset(Vector3 axis, float radius, Random random)
    {
        if (radius <= 0f)
            return Vector3.Zero;

        var normalizedAxis = axis == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(axis);

        // Same orthonormal-basis construction as RandomDirectionInCone: any vector not parallel
        // to the axis works as the seed.
        var seed = MathF.Abs(normalizedAxis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(normalizedAxis, seed));
        var v = Vector3.Cross(normalizedAxis, u);

        var r = radius * MathF.Sqrt((float)random.NextDouble());
        var theta = (float)random.NextDouble() * MathF.Tau;

        return u * (r * MathF.Cos(theta)) + v * (r * MathF.Sin(theta));
    }

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
