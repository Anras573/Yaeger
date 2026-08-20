namespace Yaeger.Graphics;

/// <summary>
/// The clock a day/night cycle runs on. Attach one to the entity that carries the scene's
/// <see cref="DirectionalLight"/>; <c>DayNightCycleSystem</c> advances it each update and writes the
/// evaluated <see cref="DirectionalLight"/> and <see cref="AmbientLight"/> back onto the same
/// entity. See docs/day-night.md.
/// </summary>
/// <remarks>
/// Nothing here is wall-clock time: <see cref="NormalizedTime"/> is the single source of truth and
/// can be assigned directly to scrub the cycle to any hour, which is why the evaluation is a pure
/// function of this component (see <c>DayNightCycle.Evaluate</c>) rather than an accumulator inside
/// the system.
/// </remarks>
public record struct TimeOfDay
{
    /// <summary>Sun straight down; the darkest point of the cycle.</summary>
    public const float Midnight = 0f;

    /// <summary>Sun on the horizon, rising.</summary>
    public const float Sunrise = 0.25f;

    /// <summary>Sun at its highest point on the arc.</summary>
    public const float Noon = 0.5f;

    /// <summary>Sun on the horizon, setting.</summary>
    public const float Sunset = 0.75f;

    /// <summary>
    /// Position in the cycle, in <c>[0, 1)</c> — see the <see cref="Midnight"/>/<see cref="Sunrise"/>
    /// /<see cref="Noon"/>/<see cref="Sunset"/> constants. Values outside the range (including
    /// negatives) are wrapped when evaluated, so advancing past the end of a day needs no special
    /// handling by callers.
    /// </summary>
    public float NormalizedTime;

    /// <summary>
    /// Real seconds one full cycle takes. Zero or negative freezes the cycle where it is — the
    /// lighting is still evaluated and applied every update, so a frozen cycle holds its look
    /// instead of going dark, and <see cref="NormalizedTime"/> can still be scrubbed by hand.
    /// </summary>
    public float DayLengthSeconds;

    /// <summary>
    /// Rotation of the whole arc about the world Y axis, in radians — which compass direction the
    /// sun rises from. At zero it rises towards +X and sets towards −X.
    /// </summary>
    public float NorthOffset;

    /// <summary>
    /// Tilt of the arc away from the vertical plane, in radians, applied about the world X axis. It
    /// leaves the horizon crossings where they are (a rising or setting sun is at elevation zero
    /// whatever the tilt) and lowers the arc's peak, so the sun passes beside the zenith rather than
    /// exactly through it. That matters beyond taste: a light pointing straight down is the
    /// degenerate case for a look-at matrix, and the shadow rig switches its up vector there.
    /// </summary>
    public float AxisTilt;

    /// <summary>
    /// Noon, a two-minute cycle, and an arc tilted ~20° off the zenith.
    /// </summary>
    public static TimeOfDay Default =>
        new()
        {
            NormalizedTime = Noon,
            DayLengthSeconds = 120f,
            NorthOffset = 0f,
            AxisTilt = 0.35f,
        };
}
