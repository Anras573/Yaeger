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

    /// <summary>
    /// Shape a particle's starting position is sampled from. <see cref="EmissionShape.Point"/>
    /// (the default) spawns every particle at the emitter's exact position, matching every
    /// existing emitter's behaviour.
    /// </summary>
    public EmissionShape Shape = EmissionShape.Point;

    /// <summary>
    /// Radius of the disc used by <see cref="EmissionShape.Disc"/>. Ignored by
    /// <see cref="EmissionShape.Point"/>, and a non-positive value collapses <c>Disc</c> back to
    /// spawning at the centre.
    /// </summary>
    public float DiscRadius;

    /// <summary>
    /// Fractional jitter applied to <see cref="InitialSpeed"/> at spawn: a particle's speed is
    /// <c>InitialSpeed * (1 + U(-SpeedVariance, SpeedVariance))</c>, clamped to non-negative.
    /// 0 (the default) is no jitter — existing emitters are unaffected.
    /// </summary>
    public float SpeedVariance;

    /// <summary>
    /// Fractional jitter applied to <see cref="ParticleLifetime"/> at spawn, same shape as
    /// <see cref="SpeedVariance"/>. Clamped away from zero so a jittered lifetime never spawns an
    /// already-expired particle.
    /// </summary>
    public float LifetimeVariance;

    /// <summary>
    /// Fractional jitter applied to a particle's <see cref="StartSize"/>/<see cref="EndSize"/> at
    /// spawn: one multiplier per particle scales both ends of its size lerp, so the lerp curve's
    /// shape is preserved and only its overall scale varies between particles — this is what
    /// breaks up a plume's otherwise-uniform silhouette.
    /// </summary>
    public float SizeVariance;

    /// <summary>
    /// When true, a particle spawns with a random billboard rotation instead of the default 0.
    /// Overridden by the velocity-projected rotation whenever a particle has meaningful
    /// screen-space velocity (see <see cref="Yaeger.Rendering.BillboardMath.ProjectVelocity"/>) —
    /// the same rotation <see cref="VelocityStretch"/> already drives for a moving/stretched
    /// particle — so this is most visible on slow-moving or near-stationary particles. Default
    /// false: existing emitters render identically.
    /// </summary>
    public bool RandomInitialRotation;

    /// <summary>
    /// Constant acceleration applied to every particle each step, in world units per second
    /// squared — gravity (negative Y), buoyancy (positive Y), or wind (a horizontal bias) all
    /// covered by one vector. Zero (the default) leaves particles at the constant velocity they
    /// spawned with, matching every existing emitter.
    /// </summary>
    public Vector3 Acceleration;

    /// <summary>
    /// Exponential velocity damping per second: each step a particle's velocity is scaled by
    /// <c>exp(-Drag * deltaTime)</c>, so it settles toward whatever terminal velocity
    /// <see cref="Acceleration"/> implies instead of coasting at spawn speed forever. 0 (the
    /// default) is no damping.
    /// </summary>
    public float Drag;

    /// <summary>
    /// Amplitude of coherent noise displacement applied to a particle's velocity each step (see
    /// <see cref="Yaeger.Graphics.ValueNoise3D"/>), breaking up otherwise perfectly straight-line
    /// motion. 0 (the default) disables turbulence entirely — no noise is sampled.
    /// </summary>
    public float Turbulence;

    /// <summary>
    /// Spatial/temporal frequency the turbulence noise field is sampled at — higher values make
    /// the perturbation change more rapidly over distance and time. Ignored while
    /// <see cref="Turbulence"/> is 0.
    /// </summary>
    public float TurbulenceFrequency = 1f;
}
