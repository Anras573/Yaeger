using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Configures continuous particle emission from an entity in 3D space. Pair it with a
/// <see cref="Transform3D"/> (the emitter position); simulation is driven by
/// <c>Yaeger.Systems.ParticleSystem3D</c> and rendering by <c>Yaeger.Systems.ParticleRenderSystem3D</c>.
/// Mirrors the 2D <see cref="ParticleEmitter"/>'s design (fixed-size pool, start/end colour and
/// size lerp) with the additions 3D billboard rendering needs: a blend mode and optional
/// velocity-aligned stretch.
/// </summary>
/// <param name="texturePath">Path of the texture each particle billboard is drawn with.</param>
public struct ParticleEmitter3D(string texturePath)
{
    /// <summary>Capacity of the emitter's particle pool. Emission stops while the pool is full.</summary>
    public int MaxParticles = 256;

    /// <summary>Particles emitted per second.</summary>
    public float EmitRate = 50f;

    /// <summary>Lifetime of each particle, in seconds.</summary>
    public float ParticleLifetime = 1f;

    /// <summary>
    /// Centre direction of emission. Does not need to be normalised; only the direction is used.
    /// When zero, +Y is used as the base direction — combine with a <see cref="SpreadAngle"/> of
    /// 2π for a full spherical spread.
    /// </summary>
    public Vector3 EmitDirection = Vector3.UnitY;

    /// <summary>
    /// Total cone angle, in radians, that emission is spread over (centred on
    /// <see cref="EmitDirection"/>) — a spawned particle's direction deviates from
    /// <see cref="EmitDirection"/> by up to half this angle, in a uniformly random direction
    /// around the axis.
    /// </summary>
    public float SpreadAngle = MathF.PI / 4f;

    /// <summary>Initial speed of emitted particles, in world units per second.</summary>
    public float InitialSpeed = 1f;

    /// <summary>Particle tint at birth.</summary>
    public Color StartColor = Color.White;

    /// <summary>Particle tint at the end of its lifetime (lerped over lifetime).</summary>
    public Color EndColor = Color.White;

    /// <summary>Particle billboard size at birth, in world units.</summary>
    public float StartSize = 0.1f;

    /// <summary>Particle billboard size at the end of its lifetime (lerped over lifetime).</summary>
    public float EndSize = 0.1f;

    /// <summary>Path of the texture each particle billboard is drawn with.</summary>
    public string TexturePath = texturePath;

    /// <summary>
    /// How particle billboards are composited. Only <see cref="MaterialBlendMode.Transparent"/>
    /// (the default — standard alpha blending, sorted back-to-front against other transparent
    /// emitters) and <see cref="MaterialBlendMode.Additive"/> (brightens the frame, order-independent,
    /// suited to sparks/embers/glows — see issue #194) are meaningful here; <c>Opaque</c>/<c>Cutout</c>
    /// are treated as <c>Transparent</c> by <c>ParticleRenderSystem3D</c>.
    /// </summary>
    public MaterialBlendMode BlendMode = MaterialBlendMode.Transparent;

    /// <summary>
    /// How far a particle's billboard elongates along its direction of travel, in world units per
    /// unit of speed. 0 (the default) keeps every billboard square/round, matching a soft puff of
    /// smoke or dust. A positive value stretches the quad into a streak aligned with velocity —
    /// suited to sparks, bolts, and tracers — while the perpendicular axis stays at the
    /// lifetime-lerped size. Has no visible effect on a stationary particle.
    /// </summary>
    public float VelocityStretch;
}
