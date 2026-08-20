using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Deterministic, continuous 3D value noise — pure math, no GL, no allocation. Backs
/// <see cref="ParticlePool3D"/>'s turbulence term (see <see cref="ParticleEmitter3D.Turbulence"/>),
/// which perturbs particle motion coherently in space and time rather than with per-frame white
/// noise (which would strobe instead of flow).
/// </summary>
public static class ValueNoise3D
{
    /// <summary>
    /// Samples the noise field at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>),
    /// returning a value in [-1, 1]. Continuous: nearby inputs produce nearby outputs, since a fixed
    /// pseudo-random value is hashed at each integer lattice point and neighbouring cells are
    /// interpolated between with a quintic ("Perlin fade") curve rather than jumping between
    /// independent random samples.
    /// </summary>
    public static float Sample(float x, float y, float z)
    {
        var xi = MathF.Floor(x);
        var yi = MathF.Floor(y);
        var zi = MathF.Floor(z);
        var u = Fade(x - xi);
        var v = Fade(y - yi);
        var w = Fade(z - zi);

        var x00 = Lerp(Hash(xi, yi, zi), Hash(xi + 1f, yi, zi), u);
        var x10 = Lerp(Hash(xi, yi + 1f, zi), Hash(xi + 1f, yi + 1f, zi), u);
        var x01 = Lerp(Hash(xi, yi, zi + 1f), Hash(xi + 1f, yi, zi + 1f), u);
        var x11 = Lerp(Hash(xi, yi + 1f, zi + 1f), Hash(xi + 1f, yi + 1f, zi + 1f), u);

        var y0 = Lerp(x00, x10, v);
        var y1 = Lerp(x01, x11, v);

        // Lerp(y0, y1, w) lands in [0, 1); remap to [-1, 1].
        return Lerp(y0, y1, w) * 2f - 1f;
    }

    /// <summary>
    /// Samples three independent (decorrelated) noise channels at <paramref name="point"/> — one
    /// per axis, each offset by a fixed constant so the X/Y/Z outputs don't move in lockstep — the
    /// shape a per-axis turbulence displacement needs.
    /// </summary>
    public static Vector3 Sample3(Vector3 point) =>
        new(
            Sample(point.X, point.Y, point.Z),
            Sample(point.Y + 19.19f, point.Z + 7.71f, point.X),
            Sample(point.Z + 41.41f, point.X + 3.13f, point.Y)
        );

    /// <summary>
    /// Samples <see cref="Sample3"/> at a point that flows continuously over
    /// <paramref name="time"/> even when <paramref name="position"/> itself is fixed: <c>position</c>
    /// and <c>time</c> are both scaled by <paramref name="frequency"/>, and <c>time</c> translates
    /// the sample point along a fixed axis before sampling. Without this, a stationary particle
    /// (zero velocity) would sample the same noise value forever instead of a perturbation that
    /// keeps varying — the field itself has to move past a still point, not just the point through
    /// the field.
    /// </summary>
    public static Vector3 SampleFlow(Vector3 position, float time, float frequency)
    {
        var point = position * frequency + new Vector3(time * frequency, 0f, 0f);
        return Sample3(point);
    }

    private static float Hash(float x, float y, float z)
    {
        var s = MathF.Sin(x * 12.9898f + y * 78.233f + z * 37.719f) * 43758.5453f;
        return s - MathF.Floor(s);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
