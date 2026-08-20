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
    /// <param name="sizeMultiplier">
    /// Per-particle size multiplier — see <see cref="Particle3D.SizeMultiplier"/>. Defaults to 1
    /// (no jitter), so existing callers are unaffected.
    /// </param>
    /// <param name="initialRotation">
    /// Per-particle spawn rotation, in radians — see <see cref="Particle3D.InitialRotation"/>.
    /// Defaults to 0, so existing callers are unaffected.
    /// </param>
    /// <param name="startFrame">
    /// Per-particle flipbook start frame — see <see cref="Particle3D.StartFrame"/>. Defaults to 0,
    /// so existing callers are unaffected.
    /// </param>
    public bool TrySpawn(
        Vector3 position,
        Vector3 velocity,
        float lifetime,
        float sizeMultiplier = 1f,
        float initialRotation = 0f,
        int startFrame = 0
    )
    {
        if (AliveCount >= _particles.Length)
            return false;

        _particles[AliveCount] = new Particle3D
        {
            Position = position,
            Velocity = velocity,
            Age = 0f,
            Lifetime = lifetime,
            SizeMultiplier = sizeMultiplier,
            InitialRotation = initialRotation,
            StartFrame = startFrame,
        };
        AliveCount++;
        return true;
    }

    /// <summary>
    /// Ages all live particles, recycles the ones whose lifetime expired, and integrates forces
    /// and velocity into position for the survivors.
    /// </summary>
    /// <param name="acceleration">
    /// Constant per-second-squared acceleration applied before position integration (see
    /// <see cref="ParticleEmitter3D.Acceleration"/>). The default (zero) is a no-op, matching this
    /// method's behaviour before forces existed.
    /// </param>
    /// <param name="drag">
    /// Exponential velocity damping per second (see <see cref="ParticleEmitter3D.Drag"/>). The
    /// default (0) is no damping.
    /// </param>
    /// <param name="turbulence">
    /// Amplitude of coherent noise velocity perturbation (see
    /// <see cref="ParticleEmitter3D.Turbulence"/>). The default (0) samples no noise at all.
    /// </param>
    /// <param name="turbulenceFrequency">
    /// Frequency the turbulence noise field is sampled at; ignored while
    /// <paramref name="turbulence"/> is 0.
    /// </param>
    public void Update(
        float deltaTime,
        Vector3 acceleration = default,
        float drag = 0f,
        float turbulence = 0f,
        float turbulenceFrequency = 1f
    )
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

            // Semi-implicit (symplectic) Euler: this step's forces update velocity first, and
            // *that* updated velocity is what integrates position below — never the velocity from
            // the start of the step. Stable under constant acceleration (unlike explicit Euler,
            // which gains energy over time) and framerate-independent for a given fixed step,
            // mirroring how PhysicsWorld2D's MovementSystem orders its own integration.
            if (acceleration != Vector3.Zero)
                particle.Velocity += acceleration * deltaTime;

            if (drag > 0f)
            {
                // exp(-drag * dt) decays velocity toward zero without ever overshooting past it
                // (unlike a linear "1 - drag * dt" term, which reverses direction once drag * dt
                // exceeds 1), and stays framerate-independent for a given wall-clock interval.
                particle.Velocity *= MathF.Exp(-drag * deltaTime);
            }

            if (turbulence != 0f)
            {
                var noise = ValueNoise3D.SampleFlow(
                    particle.Position,
                    particle.Age,
                    turbulenceFrequency
                );
                particle.Velocity += noise * (turbulence * deltaTime);
            }

            particle.Position += particle.Velocity * deltaTime;
            i++;
        }
    }
}
