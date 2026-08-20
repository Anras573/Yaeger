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
    /// <summary>
    /// Samples the flicker's intensity perturbation at <paramref name="time"/>, in <c>[-1, 1]</c>.
    /// Two octaves of <see cref="ValueNoise3D"/> are summed (the second at a higher frequency and a
    /// lower weight) so the result reads as irregular flame noise rather than a single smooth
    /// wobble — still a weighted average of two values each in <c>[-1, 1]</c>, so the sum never
    /// leaves that range. <paramref name="seed"/> offsets the sampled point so two flickers with
    /// identical <paramref name="time"/>/<paramref name="frequency"/> histories but different seeds
    /// produce uncorrelated output.
    /// </summary>
    public static float Sample(float time, float frequency, float seed)
    {
        var t = time * MathF.Max(frequency, 0f);

        var octave1 = ValueNoise3D.Sample(t, seed, 0f);
        var octave2 = ValueNoise3D.Sample(t * 2.17f + 31.7f, seed * 1.31f + 5.9f, 0f);

        return octave1 * 0.6f + octave2 * 0.4f;
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
            t * 1.31f + seed * 2.7f + 11.1f,
            t * 0.77f + seed * 4.3f + 23.3f
        );

        return ValueNoise3D.Sample3(point) * radius;
    }
}
