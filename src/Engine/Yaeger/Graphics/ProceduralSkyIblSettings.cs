namespace Yaeger.Graphics;

/// <summary>
/// Throttle tunables for <c>ProceduralSkyIbl</c>'s re-bake decision. Like
/// <see cref="ShadowSettings"/>, <c>default(ProceduralSkyIblSettings)</c> is all-zero (rebakes every
/// call) rather than sensible — start from <see cref="Default"/> and override what you need with a
/// <c>with</c> expression.
/// </summary>
public record struct ProceduralSkyIblSettings
{
    /// <summary>
    /// Minimum real seconds between re-bakes — a hard floor on re-bake rate regardless of how fast
    /// the sun is moving, which is what keeps a fast-forwarded day/night cycle to a bounded number
    /// of prefilter runs rather than one every frame. Non-positive disables the floor (every
    /// <c>Update</c> call becomes eligible, subject only to <see cref="SunDirectionThresholdDegrees"/>).
    /// </summary>
    public float MinRebakeInterval;

    /// <summary>
    /// Minimum change in sun direction, in degrees, since the last bake before a new one is
    /// considered worthwhile — once <see cref="MinRebakeInterval"/> has elapsed, this decides
    /// whether the sky has actually moved enough to be worth the GPU cost. Non-positive means any
    /// nonzero movement qualifies.
    /// </summary>
    public float SunDirectionThresholdDegrees;

    /// <summary>
    /// A 2-second floor and a 3° threshold — for a 120-second day (the default
    /// <see cref="TimeOfDay.DayLengthSeconds"/>), that's roughly a bake every couple of seconds
    /// while the sun is actively moving, comfortably bounded over a long-running cycle.
    /// </summary>
    public static ProceduralSkyIblSettings Default =>
        new() { MinRebakeInterval = 2f, SunDirectionThresholdDegrees = 3f };
}
