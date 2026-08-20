using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// The lighting a <see cref="TimeOfDay"/> evaluates to — the result of
/// <c>DayNightCycle.Evaluate</c>, and what <c>DayNightCycleSystem</c> writes onto the cycle's
/// entity each update.
/// </summary>
/// <param name="Sun">
/// The sun's directional light. Its intensity ramps up from zero at the horizon, so it is dark
/// whenever the sun is down and can be left in the scene year-round rather than switched on and off.
/// </param>
/// <param name="Moon">
/// The moon's directional light, opposite the sun and ramped the same way. Pair it with
/// <paramref name="Sun"/> — via two <see cref="CelestialLight"/> entities — to light dawn and dusk
/// with both at once.
/// </param>
/// <param name="KeyLight">
/// Whichever of <paramref name="Sun"/> and <paramref name="Moon"/> is above the horizon, for a scene
/// with a single directional light to use. Both fade to zero intensity at the crossing, so the
/// direction flip happens while the light contributes nothing — a scene never sees it swing across
/// the sky.
/// </param>
/// <param name="Ambient">Scene ambient for this moment — day, twilight, or night.</param>
/// <param name="Exposure">
/// Suggested tone-map exposure. Nothing in <c>Yaeger.Core</c> applies it — <c>ToneMapEffect</c>
/// lives in the native runtime — so a game that post-processes reads this and assigns it to its
/// effect's <c>Exposure</c>. Ignoring it is fine; the scene is simply darker at night.
/// </param>
/// <param name="SunDirection">
/// Unit vector pointing from the scene towards the sun, matching
/// <see cref="DirectionalLight.Direction"/>'s convention. Exposed separately from
/// <paramref name="KeyLight"/> so a sky renderer can place a sun disc while the moon is the key
/// light.
/// </param>
/// <param name="MoonDirection">Unit vector pointing towards the moon — directly opposite the sun.</param>
/// <param name="DaylightFactor">
/// How far through the night→day blend this moment sits, in <c>[0, 1]</c>: 0 at or below
/// <see cref="DayNightCycleSettings.NightElevation"/>, 1 at or above
/// <see cref="DayNightCycleSettings.DaylightElevation"/>. Useful for anything else that should fade
/// with the light — star visibility, lamps switching on.
/// </param>
public readonly record struct DayNightLighting(
    DirectionalLight Sun,
    DirectionalLight Moon,
    DirectionalLight KeyLight,
    AmbientLight Ambient,
    float Exposure,
    Vector3 SunDirection,
    Vector3 MoonDirection,
    float DaylightFactor
)
{
    /// <summary>Whether the sun is above the horizon — i.e. whether the key light is the sun.</summary>
    public bool IsDaytime => SunDirection.Y >= 0f;

    /// <summary>
    /// Sun elevation: the Y component of <see cref="SunDirection"/>, which is the sine of the sun's
    /// altitude above the horizon. Negative once the sun has set.
    /// </summary>
    public float SunElevation => SunDirection.Y;
}
