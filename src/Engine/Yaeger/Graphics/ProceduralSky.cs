using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Art direction and live state for a shader-computed sky: a gradient that tracks the sun, sun and
/// moon discs, a rotating star field, and drifting clouds — no cubemap assets. Attach to an entity
/// alongside a camera to render it via <c>ProceduralSkyRenderer</c>, wired into
/// <c>MeshRenderSystem</c> the same way <see cref="Skybox"/> is. The two are independent components:
/// a scene keeps using the six-image <see cref="Skybox"/> unless it opts into this one instead.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SunDirection"/>, <see cref="MoonDirection"/>, and <see cref="DaylightFactor"/> are
/// normally driven for you: when a <see cref="TimeOfDay"/> entity shares the world,
/// <c>DayNightCycleSystem</c> writes all three onto every <see cref="ProceduralSky"/> entity each
/// update — the same "auto-picked-up" relationship <c>DayNightLighting.SunDirection</c>'s remarks
/// describe ("so a sky renderer can place a sun disc while the moon is the key light"). A scene with
/// no day/night cycle can still set them by hand; they default to a fixed noon sky.
/// </para>
/// <para>
/// <see cref="Elapsed"/> is different: it is this component's own clock, advanced by
/// <c>ProceduralSkySystem</c> independently of any day/night cycle, and exists purely to drive
/// <see cref="CloudWind"/> scroll — clouds keep drifting even in a scene with a frozen or absent
/// <see cref="TimeOfDay"/>.
/// </para>
/// </remarks>
public record struct ProceduralSky
{
    /// <summary>
    /// Unit vector pointing from the scene towards the sun, matching
    /// <see cref="DirectionalLight.Direction"/>'s convention. Placed by
    /// <c>DayNightCycleSystem</c> when a <see cref="TimeOfDay"/> shares the world.
    /// </summary>
    public Vector3 SunDirection;

    /// <summary>Unit vector pointing towards the moon. Placed the same way as <see cref="SunDirection"/>.</summary>
    public Vector3 MoonDirection;

    /// <summary>
    /// Night→day blend in <c>[0, 1]</c>, matching <see cref="DayNightLighting.DaylightFactor"/>.
    /// Fades the star field out by day and fades the sun disc's glow in as it climbs.
    /// </summary>
    public float DaylightFactor;

    /// <summary>
    /// World-units-per-second wind vector clouds scroll along, in the sky dome's local (x, z)
    /// plane. Zero freezes the cloud layer in place.
    /// </summary>
    public Vector2 CloudWind;

    /// <summary>Noise frequency for the cloud layer. Higher values produce smaller, more numerous clouds.</summary>
    public float CloudScale;

    /// <summary>Fraction of the sky covered by cloud, in <c>[0, 1]</c>. 0 is a clear sky.</summary>
    public float CloudCoverage;

    /// <summary>
    /// Fraction of star-field hash cells that show a star, in <c>[0, 1)</c>. Higher is denser; 1
    /// shows none.
    /// </summary>
    public float StarDensity;

    /// <summary>
    /// Stylized moon phase in <c>[0, 1]</c>: 0 and 1 are new (dark), 0.5 is full. Not astronomically
    /// exact — see <c>ProceduralSky.frag</c> — but sweeps smoothly and symmetrically between the two.
    /// </summary>
    public float MoonPhase;

    /// <summary>
    /// Seconds of sky time accumulated so far, advanced by <c>ProceduralSkySystem</c> each update.
    /// Not meant to be set directly, aside from resetting the cloud scroll back to zero. Kept
    /// separate from <see cref="TimeOfDay.NormalizedTime"/> so clouds keep drifting regardless of
    /// whether — or how fast — the day/night cycle is running.
    /// </summary>
    public float Elapsed;

    /// <summary>
    /// A clear noon sky: sun overhead, moon opposite and below the horizon, full daylight, a light
    /// scattering of cloud, and a dense, still-imperceptible (daylight-faded) star field.
    /// </summary>
    public static ProceduralSky Default =>
        new()
        {
            SunDirection = Vector3.UnitY,
            MoonDirection = -Vector3.UnitY,
            DaylightFactor = 1f,
            CloudWind = new Vector2(0.015f, 0.01f),
            CloudScale = 2.5f,
            CloudCoverage = 0.45f,
            StarDensity = 0.985f,
            MoonPhase = 0.5f,
            Elapsed = 0f,
        };
}
