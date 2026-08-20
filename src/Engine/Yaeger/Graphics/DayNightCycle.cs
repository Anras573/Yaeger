using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Pure evaluation of a day/night cycle: <see cref="TimeOfDay"/> + <see cref="DayNightCycleSettings"/>
/// in, <see cref="DayNightLighting"/> out. No world, no GL, no accumulated state — the same inputs
/// always produce the same lighting, which is what makes the cycle scrubbable and directly
/// unit-testable (the same reason <c>MeshInstanceBatcher</c> and <c>BillboardMath</c> are static).
/// <c>DayNightCycleSystem</c> is the thin ECS wrapper that advances the clock and applies the result.
/// </summary>
public static class DayNightCycle
{
    /// <summary>
    /// Evaluates the sun/moon direction, key light, ambient, and suggested exposure for a moment in
    /// the cycle. <paramref name="time"/>'s <see cref="TimeOfDay.NormalizedTime"/> is wrapped into
    /// <c>[0, 1)</c> first, so callers may pass an unwrapped or negative value.
    /// </summary>
    public static DayNightLighting Evaluate(TimeOfDay time, DayNightCycleSettings settings)
    {
        var sunDirection = SunDirection(time);
        var moonDirection = -sunDirection;
        var elevation = sunDirection.Y;

        var daylight = DaylightFactor(elevation, settings);
        var twilight = TwilightFactor(elevation, settings);

        var ambient = LerpAmbient(
            LerpAmbient(settings.NightAmbient, settings.DayAmbient, daylight),
            settings.TwilightAmbient,
            twilight
        );

        var exposure = Lerp(
            Sanitize(settings.NightExposure),
            Sanitize(settings.DayExposure),
            daylight
        );

        return new DayNightLighting(
            KeyLight: KeyLight(sunDirection, moonDirection, settings),
            Ambient: ambient,
            Exposure: exposure,
            SunDirection: sunDirection,
            MoonDirection: moonDirection,
            DaylightFactor: daylight
        );
    }

    /// <summary>
    /// The unit vector pointing from the scene towards the sun at <paramref name="time"/>, matching
    /// <see cref="DirectionalLight.Direction"/>'s convention (towards the light, not along its
    /// travel).
    /// </summary>
    /// <remarks>
    /// The arc is built in the XY plane — rising towards +X at <see cref="TimeOfDay.Sunrise"/>,
    /// straight up at <see cref="TimeOfDay.Noon"/>, setting towards −X at
    /// <see cref="TimeOfDay.Sunset"/> — then tilted about X by <see cref="TimeOfDay.AxisTilt"/> and
    /// swung about Y by <see cref="TimeOfDay.NorthOffset"/>. Tilting about X scales only the
    /// vertical component, so the horizon crossings stay exactly at elevation zero no matter how far
    /// the arc leans.
    /// </remarks>
    public static Vector3 SunDirection(TimeOfDay time)
    {
        // Midnight sits a quarter-turn before sunrise, so the phase is offset by Sunrise: the
        // resulting angle is 0 at sunrise, +pi/2 at noon, +pi at sunset, -pi/2 at midnight.
        var angle = (Wrap01(time.NormalizedTime) - TimeOfDay.Sunrise) * MathF.Tau;
        var horizontal = MathF.Cos(angle);
        var vertical = MathF.Sin(angle);

        var tilt = Finite(time.AxisTilt);
        var direction = new Vector3(
            horizontal,
            vertical * MathF.Cos(tilt),
            vertical * MathF.Sin(tilt)
        );

        var offset = Finite(time.NorthOffset);
        if (offset != 0f)
            direction = Vector3.Transform(direction, Matrix4x4.CreateRotationY(offset));

        // Already unit length by construction; normalising guards against drift from the rotation
        // and keeps the contract explicit for callers that skip normalising themselves.
        var lengthSquared = direction.LengthSquared();
        return lengthSquared > 0f ? direction / MathF.Sqrt(lengthSquared) : Vector3.UnitY;
    }

    /// <summary>
    /// How far through the night→day blend a given sun <paramref name="elevation"/> sits, in
    /// <c>[0, 1]</c>: 0 at or below <see cref="DayNightCycleSettings.NightElevation"/>, 1 at or above
    /// <see cref="DayNightCycleSettings.DaylightElevation"/>, smoothstepped between.
    /// </summary>
    public static float DaylightFactor(float elevation, DayNightCycleSettings settings)
    {
        var night = MathF.Min(Finite(settings.NightElevation), Finite(settings.DaylightElevation));
        var day = MathF.Max(Finite(settings.NightElevation), Finite(settings.DaylightElevation));
        return SmoothStep(night, day, Finite(elevation));
    }

    /// <summary>
    /// How strongly the twilight ambient applies at a given sun <paramref name="elevation"/>:
    /// 1 with the sun exactly on the horizon, falling to 0 at either end of the twilight band.
    /// </summary>
    public static float TwilightFactor(float elevation, DayNightCycleSettings settings)
    {
        var value = Finite(elevation);
        var edge =
            value >= 0f
                ? MathF.Abs(Finite(settings.DaylightElevation))
                : MathF.Abs(Finite(settings.NightElevation));

        if (edge <= 0f)
            return value == 0f ? 1f : 0f;

        return SmoothStep(0f, 1f, 1f - Clamp01(MathF.Abs(value) / edge));
    }

    /// <summary>
    /// Wraps a normalized cycle position into <c>[0, 1)</c>, mapping negatives forward and
    /// non-finite values to 0.
    /// </summary>
    public static float Wrap01(float value)
    {
        if (!float.IsFinite(value))
            return 0f;

        var wrapped = value % 1f;
        if (wrapped < 0f)
            wrapped += 1f;

        // A tiny negative input can round up to exactly 1 when shifted; keep the range half-open.
        return wrapped >= 1f ? 0f : wrapped;
    }

    private static DirectionalLight KeyLight(
        Vector3 sunDirection,
        Vector3 moonDirection,
        DayNightCycleSettings settings
    )
    {
        // Both bodies ramp their intensity up from zero at their own horizon over the same band the
        // daylight blend uses, so whichever is chosen contributes nothing at the moment of the
        // handover.
        var rise = MathF.Abs(Finite(settings.DaylightElevation));

        if (sunDirection.Y >= 0f)
        {
            var climb = rise > 0f ? SmoothStep(0f, rise, sunDirection.Y) : 1f;
            return new DirectionalLight
            {
                Direction = sunDirection,
                Color = LerpColor(settings.SunHorizonColor, settings.SunColor, climb),
                Intensity = Sanitize(settings.SunIntensity) * climb,
            };
        }

        var moonClimb = rise > 0f ? SmoothStep(0f, rise, moonDirection.Y) : 1f;
        return new DirectionalLight
        {
            Direction = moonDirection,
            Color = settings.MoonColor,
            Intensity = Sanitize(settings.MoonIntensity) * moonClimb,
        };
    }

    private static AmbientLight LerpAmbient(AmbientLight from, AmbientLight to, float t) =>
        new()
        {
            Color = LerpColor(from.Color, to.Color, t),
            Intensity = Lerp(Sanitize(from.Intensity), Sanitize(to.Intensity), t),
        };

    // Interpolates in normalized float space rather than between byte channels, so a long blend
    // across several stops doesn't quantise on every step.
    private static Color LerpColor(Color from, Color to, float t) =>
        Color.FromVector4(Vector4.Lerp(from.ToVector4(), to.ToVector4(), Clamp01(t)));

    private static float Lerp(float from, float to, float t) => from + (to - from) * Clamp01(t);

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0)
            return value >= edge1 ? 1f : 0f;

        var t = Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float Clamp01(float value) =>
        float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);

    private static float Sanitize(float value) => float.IsFinite(value) ? MathF.Max(value, 0f) : 0f;

    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
