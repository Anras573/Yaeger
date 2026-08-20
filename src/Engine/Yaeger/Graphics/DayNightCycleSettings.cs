namespace Yaeger.Graphics;

/// <summary>
/// The tunables a <see cref="TimeOfDay"/> is evaluated against — key-light colours and intensities,
/// the ambient palette, exposure, and where along the sun's arc day becomes night. Deliberately a
/// small set of stops rather than an arbitrary keyframe track, in the same spirit as
/// <see cref="AnimationStateMachine"/>: enough to art-direct a cycle, not a curve editor.
/// </summary>
/// <remarks>
/// Like <see cref="ShadowSettings"/>, <c>default(DayNightCycleSettings)</c> is all-zero (a black
/// scene) rather than sensible — start from <see cref="Default"/> and override what you need with a
/// <c>with</c> expression.
/// </remarks>
public record struct DayNightCycleSettings
{
    /// <summary>Sun colour with the sun high in the sky.</summary>
    public Color SunColor;

    /// <summary>
    /// Sun colour as it reaches the horizon. Blended towards <see cref="SunColor"/> as the sun
    /// climbs, which is what makes a sunrise read as warm without a separate "golden hour" concept.
    /// </summary>
    public Color SunHorizonColor;

    /// <summary>Peak intensity of the sun, reached at and above <see cref="DaylightElevation"/>.</summary>
    public float SunIntensity;

    /// <summary>Moon colour. The moon is treated as a full moon opposite the sun.</summary>
    public Color MoonColor;

    /// <summary>Peak intensity of the moon. Typically a small fraction of <see cref="SunIntensity"/>.</summary>
    public float MoonIntensity;

    /// <summary>Ambient with the sun high in the sky.</summary>
    public AmbientLight DayAmbient;

    /// <summary>
    /// Ambient at the horizon crossings, blended in as the sun approaches elevation zero from
    /// either side. This is the term that carries dawn and dusk: the key light is at its dimmest
    /// exactly then, so almost everything visible is ambient.
    /// </summary>
    public AmbientLight TwilightAmbient;

    /// <summary>Ambient with the sun fully below the horizon.</summary>
    public AmbientLight NightAmbient;

    /// <summary>Suggested tone-map exposure during full daylight.</summary>
    public float DayExposure;

    /// <summary>
    /// Suggested tone-map exposure at night — normally higher than <see cref="DayExposure"/>, since
    /// a night lit at a fraction of daylight intensity otherwise reads as a black frame.
    /// </summary>
    public float NightExposure;

    /// <summary>
    /// Sun elevation (the Y component of the direction to the sun, i.e. the sine of its altitude) at
    /// or above which it is fully day. Also the width of the ramp the sun's intensity and colour
    /// climb over after sunrise.
    /// </summary>
    public float DaylightElevation;

    /// <summary>
    /// Sun elevation at or below which it is fully night — negative. The band between this and
    /// <see cref="DaylightElevation"/> is the twilight the ambient blends across.
    /// </summary>
    public float NightElevation;

    /// <summary>
    /// A temperate clear-sky cycle: a warm horizon sun that whitens as it climbs, a dim blue moon,
    /// and an ambient running blue-grey daylight → warm twilight → deep blue night.
    /// </summary>
    public static DayNightCycleSettings Default =>
        new()
        {
            SunColor = new Color(255, 250, 240),
            SunHorizonColor = new Color(255, 145, 70),
            SunIntensity = 3f,
            MoonColor = new Color(170, 195, 255),
            MoonIntensity = 0.25f,
            DayAmbient = new AmbientLight { Color = new Color(180, 205, 255), Intensity = 0.30f },
            TwilightAmbient = new AmbientLight
            {
                Color = new Color(255, 160, 110),
                Intensity = 0.12f,
            },
            NightAmbient = new AmbientLight { Color = new Color(90, 120, 190), Intensity = 0.03f },
            DayExposure = 1f,
            NightExposure = 2.5f,
            DaylightElevation = 0.25f,
            NightElevation = -0.2f,
        };
}
