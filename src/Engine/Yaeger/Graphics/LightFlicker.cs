namespace Yaeger.Graphics;

/// <summary>
/// Irregular, per-entity intensity noise for fire/torch-style <see cref="PointLight"/>/
/// <see cref="SpotLight"/>s. Attach alongside whichever light component(s) the entity carries;
/// <c>LightFlickerSystem</c> writes each update's sampled intensity onto them, and — when
/// <see cref="PositionJitter"/> is nonzero — a small positional offset onto the entity's
/// <see cref="Transform3D"/>.
/// </summary>
/// <remarks>
/// <para>
/// The signal is continuous, irregular noise (see <see cref="LightFlickerSignal"/>), not a smooth
/// periodic pulse — a <c>Tween</c> ping-ponging <c>PointLightIntensity</c> reads as a breathing
/// lamp, not fire. <see cref="Seed"/> is what keeps two braziers from flickering in lockstep: two
/// <see cref="LightFlicker"/>s with otherwise identical settings but different seeds sample
/// independent noise streams.
/// </para>
/// <para>
/// Removing this component (or destroying the entity) restores the light to
/// <see cref="BaseIntensity"/> and undoes any applied position offset, rather than leaving it at
/// whatever the last sampled frame happened to land on.
/// </para>
/// </remarks>
public record struct LightFlicker
{
    /// <summary>
    /// Intensity the light flickers around, and what it's restored to if this component is
    /// removed.
    /// </summary>
    public float BaseIntensity;

    /// <summary>
    /// How far intensity swings from <see cref="BaseIntensity"/>: sampled intensity is
    /// <c>BaseIntensity + noise * Amplitude</c> (clamped to non-negative), where <c>noise</c> is in
    /// <c>[-1, 1]</c>.
    /// </summary>
    public float Amplitude;

    /// <summary>How quickly the flicker evolves. Higher values flicker faster.</summary>
    public float Frequency;

    /// <summary>
    /// Distinguishes this light's noise stream from another's — any float works, there's no seed
    /// pool to draw from. Two <see cref="LightFlicker"/>s with identical
    /// <see cref="BaseIntensity"/>/<see cref="Amplitude"/>/<see cref="Frequency"/> but different
    /// seeds flicker independently instead of moving in lockstep.
    /// </summary>
    public float Seed;

    /// <summary>
    /// Radius, in world units, of a small positional offset applied to the light's
    /// <see cref="Transform3D"/> each update — real flames move their apparent source. 0 (the
    /// default) disables positional jitter entirely; the light flickers in intensity only.
    /// </summary>
    public float PositionJitter;

    /// <summary>
    /// Seconds of flicker time accumulated so far. Advanced by <c>LightFlickerSystem</c> each
    /// update — not meant to be set directly, aside from resetting an effect back to zero. The
    /// signal is a pure function of this value, which is what makes the flicker exactly
    /// framerate-independent: the same elapsed wall-clock time always samples the same intensity,
    /// regardless of how many steps it took to get there.
    /// </summary>
    public float Elapsed;

    /// <summary>A moderate, fast fire flicker with no positional jitter.</summary>
    public static LightFlicker Default =>
        new()
        {
            BaseIntensity = 1f,
            Amplitude = 0.3f,
            Frequency = 3f,
            Seed = 0f,
            PositionJitter = 0f,
            Elapsed = 0f,
        };
}
