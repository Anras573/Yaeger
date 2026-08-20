namespace Yaeger.Graphics;

/// <summary>
/// Where across a <see cref="ParticleEmitter3D"/>'s area a spawned particle's starting position is
/// sampled from, relative to the emitter's <see cref="Transform3D"/>.
/// </summary>
public enum EmissionShape
{
    /// <summary>Every particle spawns at the emitter's exact position. The default.</summary>
    Point,

    /// <summary>
    /// Particles spawn uniformly across a disc of <see cref="ParticleEmitter3D.DiscRadius"/>,
    /// oriented perpendicular to <see cref="ParticleEmitter3D.EmitDirection"/> — a fire filling a
    /// bowl rather than a jet from a point.
    /// </summary>
    Disc,
}
