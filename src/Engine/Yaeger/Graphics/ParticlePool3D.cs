using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Fixed-size pool of <see cref="Particle3D"/> structs backing one 3D emitter. Live particles
/// occupy the first <see cref="AliveCount"/> slots; expired particles are recycled in-place by
/// swapping the last live particle into their slot, so simulation never allocates after
/// construction. Mirrors <see cref="ParticlePool"/> with <see cref="Vector3"/> position/velocity.
/// </summary>
public sealed class ParticlePool3D
{
    private readonly Particle3D[] _particles;

    public ParticlePool3D(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _particles = new Particle3D[capacity];
    }

    /// <summary>Maximum number of simultaneously live particles.</summary>
    public int Capacity => _particles.Length;

    /// <summary>Number of currently live particles.</summary>
    public int AliveCount { get; private set; }

    /// <summary>
    /// Fractional particles carried over between frames so that emit rates lower than
    /// the frame rate still emit at the configured average. Managed by the particle system.
    /// </summary>
    public float EmissionAccumulator { get; set; }

    /// <summary>Read-only access to a live particle by index (0 ≤ index &lt; <see cref="AliveCount"/>).</summary>
    public ref readonly Particle3D this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, AliveCount);
            return ref _particles[index];
        }
    }

    /// <summary>
    /// Spawns a particle with age zero, or returns false when the pool is full.
    /// </summary>
    public bool TrySpawn(Vector3 position, Vector3 velocity, float lifetime)
    {
        if (AliveCount >= _particles.Length)
            return false;

        _particles[AliveCount] = new Particle3D
        {
            Position = position,
            Velocity = velocity,
            Age = 0f,
            Lifetime = lifetime,
        };
        AliveCount++;
        return true;
    }

    /// <summary>
    /// Ages all live particles, recycles the ones whose lifetime expired, and integrates
    /// velocity into position for the survivors.
    /// </summary>
    public void Update(float deltaTime)
    {
        var i = 0;
        while (i < AliveCount)
        {
            ref var particle = ref _particles[i];
            particle.Age += deltaTime;
            if (particle.Age >= particle.Lifetime)
            {
                // Swap-remove: the particle moved into this slot has not been aged yet
                // this frame, so re-process index i instead of advancing.
                _particles[i] = _particles[AliveCount - 1];
                AliveCount--;
                continue;
            }

            particle.Position += particle.Velocity * deltaTime;
            i++;
        }
    }
}
