using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A single live particle inside a <see cref="ParticlePool3D"/>. Colour and size are not stored
/// per particle; they are derived from the owning <see cref="ParticleEmitter3D"/> using
/// <see cref="NormalizedAge"/>, mirroring the 2D <see cref="Particle"/>.
/// </summary>
public struct Particle3D
{
    public Vector3 Position;
    public Vector3 Velocity;

    /// <summary>Seconds since the particle was spawned.</summary>
    public float Age;

    /// <summary>Total lifetime in seconds; the particle is recycled once <see cref="Age"/> reaches it.</summary>
    public float Lifetime;

    /// <summary>
    /// Per-particle multiplier applied to the owning emitter's <see cref="ParticleEmitter3D.StartSize"/>/
    /// <see cref="ParticleEmitter3D.EndSize"/> (see <see cref="ParticleEmitter3D.SizeVariance"/>).
    /// 1 leaves the emitter's lerped size untouched — the value every particle spawns with when
    /// <see cref="ParticleEmitter3D.SizeVariance"/> is 0.
    /// </summary>
    public float SizeMultiplier;

    /// <summary>
    /// Billboard rotation, in radians, this particle spawned with (see
    /// <see cref="ParticleEmitter3D.RandomInitialRotation"/>). Only visible while the particle has
    /// no meaningful screen-space velocity to project a rotation from instead — see
    /// <c>ParticleRenderSystem3D.DrawEmitter</c>.
    /// </summary>
    public float InitialRotation;

    /// <summary>
    /// Flipbook frame index this particle spawned on (see
    /// <see cref="ParticleEmitter3D.RandomStartFrame"/>). 0 — frame 0 — for every particle when
    /// that field is false, which is what keeps a non-animated 1×1 grid rendering the same way it
    /// always has.
    /// </summary>
    public int StartFrame;

    /// <summary>Age as a fraction of lifetime, clamped to [0, 1].</summary>
    public readonly float NormalizedAge => Lifetime > 0f ? Math.Clamp(Age / Lifetime, 0f, 1f) : 1f;
}
