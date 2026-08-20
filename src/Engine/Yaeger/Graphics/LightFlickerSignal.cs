using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Pure evaluation of a <see cref="LightFlicker"/>'s noise: elapsed time in, an intensity/position
/// perturbation out. No world, no GL, no accumulated state of its own — the same inputs always
/// produce the same output, which is what makes the signal directly unit-testable for range,
/// continuity, and framerate independence (the same reasoning as <see cref="DayNightCycle"/>'s split
/// from <c>DayNightCycleSystem</c>). Built on <see cref="ValueNoise3D"/>, the same continuous noise
/// primitive <c>ParticlePool3D</c>'s turbulence term uses.
/// </summary>
public static class LightFlickerSignal
{
    // Octave 2 runs faster than octave 1 and contributes less, which is what makes the sum read
    // as irregular flame noise instead of a single smooth wobble — a higher-frequency ripple
    // riding on top of a slower base wave. The weights sum to 1 so the result stays a weighted
    // average of two values each in [-1, 1], never leaving that range.
    private const float Octave1Weight = 0.6f;
    private const float Octave2Weight = 0.4f;
    private const float Octave2FrequencyScale = 2.17f;

    // Every constant below just needs to shift a noise sample's input coordinate somewhere else
    // in the field — the specific values are arbitrary, deliberately not round numbers or shared
    // ratios of each other, so octave 2 and the offset's three axes don't sample points that
    // happen to move in lockstep with octave 1 or with each other.
    private const float Octave2TimeOffset = 31.7f;
    private const float Octave2SeedScale = 1.31f;
    private const float Octave2SeedOffset = 5.9f;

    private const float OffsetYTimeScale = 1.31f;
    private const float OffsetYSeedScale = 2.7f;
    private const float OffsetYConstant = 11.1f;
    private const float OffsetZTimeScale = 0.77f;
    private const float OffsetZSeedScale = 4.3f;
    private const float OffsetZConstant = 23.3f;

    /// <summary>
    /// Samples the flicker's intensity perturbation at <paramref name="time"/>, in <c>[-1, 1]</c>.
    /// Two octaves of <see cref="ValueNoise3D"/> are summed (the second at a higher frequency and a
    /// lower weight) so the result reads as irregular flame noise rather than a single smooth
    /// wobble. <paramref name="seed"/> offsets the sampled point so two flickers with identical
    /// <paramref name="time"/>/<paramref name="frequency"/> histories but different seeds produce
    /// uncorrelated output.
    /// </summary>
    public static float Sample(float time, float frequency, float seed)
    {
        var t = time * MathF.Max(frequency, 0f);

        var octave1 = ValueNoise3D.Sample(t, seed, 0f);
        var octave2 = ValueNoise3D.Sample(
            t * Octave2FrequencyScale + Octave2TimeOffset,
            seed * Octave2SeedScale + Octave2SeedOffset,
            0f
        );

        return octave1 * Octave1Weight + octave2 * Octave2Weight;
    }

    /// <summary>
    /// Samples a small positional offset at <paramref name="time"/>, bounded to
    /// <c>[-radius, radius]</c> per axis — the shape a light "moving" as it flickers needs. Uses the
    /// same <paramref name="time"/>/<paramref name="frequency"/>/<paramref name="seed"/> inputs as
    /// <see cref="Sample"/> so position and intensity noise evolve at the same rate, offset onto
    /// three decorrelated axes via <see cref="ValueNoise3D.Sample3"/>. Returns
    /// <see cref="Vector3.Zero"/> for a non-positive <paramref name="radius"/>.
    /// </summary>
    public static Vector3 SampleOffset(float time, float frequency, float seed, float radius)
    {
        if (radius <= 0f)
            return Vector3.Zero;

        var t = time * MathF.Max(frequency, 0f);
        var point = new Vector3(
            t + seed,
            t * OffsetYTimeScale + seed * OffsetYSeedScale + OffsetYConstant,
            t * OffsetZTimeScale + seed * OffsetZSeedScale + OffsetZConstant
        );

        return ValueNoise3D.Sample3(point) * radius;
    }
}
