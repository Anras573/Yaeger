using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Configures continuous particle emission from an entity. Pair it with a
/// <see cref="Transform2D"/> (the emitter position); simulation and rendering are
/// driven by <c>Yaeger.Systems.ParticleSystem</c>.
/// </summary>
/// <param name="texturePath">Path of the texture each particle is drawn with.</param>
public struct ParticleEmitter(string texturePath)
{
    /// <summary>Capacity of the emitter's particle pool. Emission stops while the pool is full.</summary>
    public int MaxParticles = 256;

    /// <summary>Particles emitted per second.</summary>
    public float EmitRate = 50f;

    /// <summary>Lifetime of each particle, in seconds.</summary>
    public float ParticleLifetime = 1f;

    /// <summary>
    /// Centre direction of emission. Does not need to be normalised; only the angle is used.
    /// When zero, the +X axis is used as the base direction — combine with a
    /// <see cref="SpreadAngle"/> of 2π for radial emission.
    /// </summary>
    public Vector2 EmitDirection = new(0f, 1f);

    /// <summary>Total arc, in radians, that emission is spread over (centred on <see cref="EmitDirection"/>).</summary>
    public float SpreadAngle = MathF.PI / 4f;

    /// <summary>Initial speed of emitted particles, in world units per second.</summary>
    public float InitialSpeed = 1f;

    /// <summary>Particle tint at birth.</summary>
    public Color StartColor = Color.White;

    /// <summary>Particle tint at the end of its lifetime (lerped over lifetime).</summary>
    public Color EndColor = Color.White;

    /// <summary>Particle quad size at birth, in world units.</summary>
    public float StartSize = 0.1f;

    /// <summary>Particle quad size at the end of its lifetime (lerped over lifetime).</summary>
    public float EndSize = 0.1f;

    /// <summary>Path of the texture each particle is drawn with.</summary>
    public string TexturePath = texturePath;
}
