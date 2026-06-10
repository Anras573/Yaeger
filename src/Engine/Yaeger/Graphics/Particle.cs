using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// A single live particle inside a <see cref="ParticlePool"/>. Colour and size are not
/// stored per particle; they are derived from the owning <see cref="ParticleEmitter"/>
/// using <see cref="NormalizedAge"/>.
/// </summary>
public struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;

    /// <summary>Seconds since the particle was spawned.</summary>
    public float Age;

    /// <summary>Total lifetime in seconds; the particle is recycled once <see cref="Age"/> reaches it.</summary>
    public float Lifetime;

    /// <summary>Age as a fraction of lifetime, clamped to [0, 1].</summary>
    public readonly float NormalizedAge => Lifetime > 0f ? Math.Clamp(Age / Lifetime, 0f, 1f) : 1f;
}
