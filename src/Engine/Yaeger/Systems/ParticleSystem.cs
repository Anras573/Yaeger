using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Systems;

/// <summary>
/// Simulates and renders particles for every entity carrying a <see cref="ParticleEmitter"/>
/// and a <see cref="Transform2D"/>. Each emitter owns a fixed-size <see cref="ParticlePool"/>
/// whose particle storage and recycling never allocate after construction; the remaining
/// steady-state cost is the ECS query enumerators shared by every system.
///
/// Call <see cref="Update"/> once per frame from the update loop, then <see cref="Render"/>
/// from the render callback after the main render system has drawn (particles are flushed
/// through the renderer's existing batched quad path and appear on top).
/// </summary>
public class ParticleSystem : IUpdateSystem
{
    private readonly World _world;
    private readonly IRenderSurface? _renderer;
    private readonly Random _random;
    private readonly Dictionary<Entity, ParticlePool> _pools = new();
    private readonly List<Entity> _expiredPools = [];

    /// <param name="world">The world queried for emitter entities.</param>
    /// <param name="renderer">
    /// Render surface particles are submitted to. Optional so the simulation can run headless
    /// (e.g. in tests or on a server); <see cref="Render"/> is a no-op without it.
    /// </param>
    /// <param name="seed">Optional seed for the emission spread randomness, for deterministic runs.</param>
    public ParticleSystem(World world, IRenderSurface? renderer = null, int? seed = null)
    {
        _world = world;
        _renderer = renderer;
        _random = seed is null ? new Random() : new Random(seed.Value);
    }

    /// <summary>
    /// Advances all live particles, recycles expired ones, and emits new particles
    /// according to each emitter's <see cref="ParticleEmitter.EmitRate"/>.
    /// </summary>
    public void Update(float deltaTime)
    {
        foreach (
            (Entity entity, ParticleEmitter emitter, Transform2D transform) in _world.Query<
                ParticleEmitter,
                Transform2D
            >()
        )
        {
            if (emitter.MaxParticles <= 0)
                continue;

            var pool = GetOrCreatePool(entity, emitter.MaxParticles);
            pool.Update(deltaTime);
            Emit(pool, in emitter, transform.Position, deltaTime);
        }

        RemoveExpiredPools();
    }

    /// <summary>
    /// Submits one quad per live particle via the render surface's batched path, with colour
    /// and size lerped from start to end values over each particle's lifetime, then flushes.
    /// No-op when the system was constructed without a renderer.
    /// </summary>
    public void Render()
    {
        if (_renderer is null)
            return;

        foreach (
            (Entity entity, ParticleEmitter emitter) in _world.GetStore<ParticleEmitter>().All()
        )
        {
            if (!_pools.TryGetValue(entity, out var pool))
                continue;

            var startColor = emitter.StartColor.ToVector4();
            var endColor = emitter.EndColor.ToVector4();

            for (var i = 0; i < pool.AliveCount; i++)
            {
                ref readonly var particle = ref pool[i];
                var t = particle.NormalizedAge;
                var size = emitter.StartSize + (emitter.EndSize - emitter.StartSize) * t;
                var color = Vector4.Lerp(startColor, endColor, t);
                var model =
                    Matrix4x4.CreateScale(size, size, 1f)
                    * Matrix4x4.CreateTranslation(particle.Position.X, particle.Position.Y, 0f);

                _renderer.SubmitQuad(model, emitter.TexturePath, color);
            }
        }

        _renderer.FlushQueuedQuads();
    }

    /// <summary>
    /// Exposes the pool backing <paramref name="entity"/>'s emitter, primarily for
    /// diagnostics and tests. A pool exists only after the first <see cref="Update"/>.
    /// </summary>
    public bool TryGetPool(
        Entity entity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ParticlePool? pool
    ) => _pools.TryGetValue(entity, out pool);

    private ParticlePool GetOrCreatePool(Entity entity, int maxParticles)
    {
        // Recreate the pool when MaxParticles changes so the emitter component stays
        // the single source of truth for capacity.
        if (_pools.TryGetValue(entity, out var pool) && pool.Capacity == maxParticles)
            return pool;

        pool = new ParticlePool(maxParticles);
        _pools[entity] = pool;
        return pool;
    }

    private void Emit(
        ParticlePool pool,
        in ParticleEmitter emitter,
        Vector2 origin,
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

    private Vector2 RandomVelocity(in ParticleEmitter emitter)
    {
        var baseAngle =
            emitter.EmitDirection == Vector2.Zero
                ? 0f
                : MathF.Atan2(emitter.EmitDirection.Y, emitter.EmitDirection.X);
        var angle = baseAngle + ((float)_random.NextDouble() - 0.5f) * emitter.SpreadAngle;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * emitter.InitialSpeed;
    }

    private void RemoveExpiredPools()
    {
        // An emitter is only simulated while it carries both components, so a pool whose
        // entity lost either one would otherwise be retained (and rendered) forever.
        var emitterStore = _world.GetStore<ParticleEmitter>();
        var transformStore = _world.GetStore<Transform2D>();
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
