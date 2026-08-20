using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

/// <summary>
/// Tests for <see cref="DayNightCycle"/>'s pure evaluation — the sun's arc, the day/twilight
/// blends, and the sun→moon handover. No world and no GL context involved.
/// </summary>
public class DayNightCycleTests
{
    private static TimeOfDay At(
        float normalizedTime,
        float axisTilt = 0f,
        float northOffset = 0f
    ) =>
        new()
        {
            NormalizedTime = normalizedTime,
            DayLengthSeconds = 60f,
            AxisTilt = axisTilt,
            NorthOffset = northOffset,
        };

    // ── The sun's arc ────────────────────────────────────────────────────────

    [Fact]
    public void SunDirection_AtNoon_PointsUp()
    {
        var direction = DayNightCycle.SunDirection(At(TimeOfDay.Noon));

        Assert.Equal(0f, direction.X, precision: 5);
        Assert.Equal(1f, direction.Y, precision: 5);
    }

    [Fact]
    public void SunDirection_AtMidnight_PointsDown()
    {
        var direction = DayNightCycle.SunDirection(At(TimeOfDay.Midnight));

        Assert.Equal(-1f, direction.Y, precision: 5);
    }

    [Fact]
    public void SunDirection_AtSunrise_IsOnTheHorizonToTheEast()
    {
        var direction = DayNightCycle.SunDirection(At(TimeOfDay.Sunrise));

        Assert.Equal(0f, direction.Y, precision: 5);
        Assert.Equal(1f, direction.X, precision: 5);
    }

    [Fact]
    public void SunDirection_AtSunset_IsOnTheHorizonToTheWest()
    {
        var direction = DayNightCycle.SunDirection(At(TimeOfDay.Sunset));

        Assert.Equal(0f, direction.Y, precision: 5);
        Assert.Equal(-1f, direction.X, precision: 5);
    }

    [Fact]
    public void SunDirection_IsAlwaysUnitLength()
    {
        for (var step = 0; step <= 64; step++)
        {
            var direction = DayNightCycle.SunDirection(At(step / 64f, axisTilt: 0.4f));
            Assert.Equal(1f, direction.Length(), precision: 4);
        }
    }

    [Fact]
    public void SunDirection_AxisTilt_LowersTheArcWithoutMovingTheHorizonCrossings()
    {
        var tiltedNoon = DayNightCycle.SunDirection(At(TimeOfDay.Noon, axisTilt: 0.35f));
        var tiltedSunrise = DayNightCycle.SunDirection(At(TimeOfDay.Sunrise, axisTilt: 0.35f));

        // The peak is pulled off the zenith...
        Assert.True(tiltedNoon.Y < 1f);
        Assert.True(tiltedNoon.Z > 0f);
        // ...but sunrise is still exactly on the horizon.
        Assert.Equal(0f, tiltedSunrise.Y, precision: 5);
    }

    [Fact]
    public void SunDirection_NorthOffset_RotatesTheArcAboutY()
    {
        var rotated = DayNightCycle.SunDirection(At(TimeOfDay.Sunrise, northOffset: MathF.PI / 2f));

        // A quarter turn moves sunrise from +X to -Z.
        Assert.Equal(0f, rotated.X, precision: 5);
        Assert.Equal(-1f, rotated.Z, precision: 5);
    }

    [Fact]
    public void SunDirection_NonFiniteInputs_FallBackToZero()
    {
        var direction = DayNightCycle.SunDirection(
            new TimeOfDay
            {
                NormalizedTime = float.NaN,
                AxisTilt = float.NaN,
                NorthOffset = float.PositiveInfinity,
            }
        );

        // NaN time wraps to 0 (midnight) and the non-finite angles are ignored.
        Assert.Equal(-1f, direction.Y, precision: 5);
    }

    // ── Wrapping ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 0f)]
    [InlineData(2.5f, 0.5f)]
    [InlineData(-0.25f, 0.75f)]
    [InlineData(-3.5f, 0.5f)]
    public void Wrap01_MapsIntoTheHalfOpenUnitRange(float input, float expected)
    {
        Assert.Equal(expected, DayNightCycle.Wrap01(input), precision: 5);
    }

    [Fact]
    public void Wrap01_NonFinite_ReturnsZero()
    {
        Assert.Equal(0f, DayNightCycle.Wrap01(float.NaN));
        Assert.Equal(0f, DayNightCycle.Wrap01(float.PositiveInfinity));
    }

    [Fact]
    public void Evaluate_WrappedAndUnwrappedTimes_Match()
    {
        var settings = DayNightCycleSettings.Default;

        var wrapped = DayNightCycle.Evaluate(At(0.3f), settings);
        var unwrapped = DayNightCycle.Evaluate(At(3.3f), settings);

        // Not bit-exact: 3.3f % 1f and 0.3f differ in the last ulp, which is fine for lighting but
        // rules out struct equality here.
        Assert.Equal(wrapped.SunDirection.X, unwrapped.SunDirection.X, precision: 5);
        Assert.Equal(wrapped.SunDirection.Y, unwrapped.SunDirection.Y, precision: 5);
        Assert.Equal(wrapped.SunDirection.Z, unwrapped.SunDirection.Z, precision: 5);
        Assert.Equal(wrapped.KeyLight.Intensity, unwrapped.KeyLight.Intensity, precision: 5);
        Assert.Equal(wrapped.Ambient.Intensity, unwrapped.Ambient.Intensity, precision: 5);
        Assert.Equal(wrapped.Exposure, unwrapped.Exposure, precision: 5);
    }

    // ── Blend factors ────────────────────────────────────────────────────────

    [Fact]
    public void DaylightFactor_SpansNightToDayAcrossTheTwilightBand()
    {
        var settings = DayNightCycleSettings.Default;

        Assert.Equal(0f, DayNightCycle.DaylightFactor(settings.NightElevation, settings));
        Assert.Equal(0f, DayNightCycle.DaylightFactor(-1f, settings));
        Assert.Equal(1f, DayNightCycle.DaylightFactor(settings.DaylightElevation, settings));
        Assert.Equal(1f, DayNightCycle.DaylightFactor(1f, settings));

        var horizon = DayNightCycle.DaylightFactor(0f, settings);
        Assert.InRange(horizon, 0.01f, 0.99f);
    }

    [Fact]
    public void DaylightFactor_IsMonotonic()
    {
        var settings = DayNightCycleSettings.Default;
        var previous = DayNightCycle.DaylightFactor(-1f, settings);

        for (var step = 1; step <= 100; step++)
        {
            var current = DayNightCycle.DaylightFactor(-1f + step * 0.02f, settings);
            Assert.True(current >= previous, $"daylight factor dipped at step {step}");
            previous = current;
        }
    }

    [Fact]
    public void TwilightFactor_PeaksOnTheHorizonAndFallsOffBothWays()
    {
        var settings = DayNightCycleSettings.Default;

        Assert.Equal(1f, DayNightCycle.TwilightFactor(0f, settings), precision: 5);
        Assert.Equal(0f, DayNightCycle.TwilightFactor(settings.DaylightElevation, settings));
        Assert.Equal(0f, DayNightCycle.TwilightFactor(settings.NightElevation, settings));
        Assert.Equal(0f, DayNightCycle.TwilightFactor(1f, settings));
        Assert.Equal(0f, DayNightCycle.TwilightFactor(-1f, settings));
    }

    // ── Key light ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_AtNoon_KeyLightIsTheSunAtFullIntensity()
    {
        var settings = DayNightCycleSettings.Default;

        var lighting = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);

        Assert.True(lighting.IsDaytime);
        Assert.Equal(settings.SunIntensity, lighting.KeyLight.Intensity, precision: 4);
        Assert.Equal(lighting.SunDirection, lighting.KeyLight.Direction);
        Assert.Equal(settings.SunColor, lighting.KeyLight.Color);
    }

    [Fact]
    public void Evaluate_AtMidnight_KeyLightIsTheMoon()
    {
        var settings = DayNightCycleSettings.Default;

        var lighting = DayNightCycle.Evaluate(At(TimeOfDay.Midnight), settings);

        Assert.False(lighting.IsDaytime);
        Assert.Equal(settings.MoonColor, lighting.KeyLight.Color);
        Assert.Equal(lighting.MoonDirection, lighting.KeyLight.Direction);
        Assert.Equal(settings.MoonIntensity, lighting.KeyLight.Intensity, precision: 4);
        Assert.True(lighting.KeyLight.Intensity < settings.SunIntensity);
    }

    [Fact]
    public void Evaluate_MoonIsOppositeTheSun()
    {
        var lighting = DayNightCycle.Evaluate(
            At(0.4f, axisTilt: 0.3f),
            DayNightCycleSettings.Default
        );

        Assert.Equal(-lighting.SunDirection.X, lighting.MoonDirection.X, precision: 5);
        Assert.Equal(-lighting.SunDirection.Y, lighting.MoonDirection.Y, precision: 5);
        Assert.Equal(-lighting.SunDirection.Z, lighting.MoonDirection.Z, precision: 5);
    }

    [Fact]
    public void Evaluate_NearTheHorizon_SunIsWarmerAndDimmerThanAtNoon()
    {
        var settings = DayNightCycleSettings.Default;

        var dawn = DayNightCycle.Evaluate(At(TimeOfDay.Sunrise + 0.01f), settings);
        var noon = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);

        Assert.True(dawn.KeyLight.Intensity < noon.KeyLight.Intensity);
        Assert.True(dawn.KeyLight.Color.R >= dawn.KeyLight.Color.B);
        Assert.True(dawn.KeyLight.Color.B < noon.KeyLight.Color.B);
    }

    [Fact]
    public void Evaluate_AcrossTheHorizonCrossing_KeyLightIntensityStaysContinuous()
    {
        var settings = DayNightCycleSettings.Default;

        // The direction flips from sun to moon exactly at sunset; the intensity must be ~0 on both
        // sides of it, or the scene sees the key light jump across the sky at full strength.
        var justBefore = DayNightCycle.Evaluate(At(TimeOfDay.Sunset - 0.0005f), settings);
        var justAfter = DayNightCycle.Evaluate(At(TimeOfDay.Sunset + 0.0005f), settings);

        Assert.True(justBefore.IsDaytime);
        Assert.False(justAfter.IsDaytime);
        Assert.True(justBefore.KeyLight.Intensity < 0.01f);
        Assert.True(justAfter.KeyLight.Intensity < 0.01f);
    }

    [Fact]
    public void Evaluate_SweepingAFullCycle_NeverProducesNonFiniteOrNegativeLighting()
    {
        var settings = DayNightCycleSettings.Default;

        for (var step = 0; step < 360; step++)
        {
            var lighting = DayNightCycle.Evaluate(At(step / 360f, axisTilt: 0.35f), settings);

            Assert.True(float.IsFinite(lighting.KeyLight.Intensity));
            Assert.True(lighting.KeyLight.Intensity >= 0f);
            Assert.True(float.IsFinite(lighting.Ambient.Intensity));
            Assert.True(lighting.Ambient.Intensity >= 0f);
            Assert.True(float.IsFinite(lighting.Exposure));
            Assert.InRange(lighting.DaylightFactor, 0f, 1f);
            Assert.Equal(1f, lighting.KeyLight.Direction.Length(), precision: 4);
        }
    }

    // ── Ambient and exposure ─────────────────────────────────────────────────

    [Fact]
    public void Evaluate_AmbientIsBrighterByDayThanByNight()
    {
        var settings = DayNightCycleSettings.Default;

        var noon = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);
        var midnight = DayNightCycle.Evaluate(At(TimeOfDay.Midnight), settings);

        Assert.Equal(settings.DayAmbient.Intensity, noon.Ambient.Intensity, precision: 4);
        Assert.Equal(settings.NightAmbient.Intensity, midnight.Ambient.Intensity, precision: 4);
        Assert.True(noon.Ambient.Intensity > midnight.Ambient.Intensity);
    }

    [Fact]
    public void Evaluate_OnTheHorizon_AmbientIsTheTwilightStop()
    {
        var settings = DayNightCycleSettings.Default;

        var dusk = DayNightCycle.Evaluate(At(TimeOfDay.Sunset), settings);

        Assert.Equal(settings.TwilightAmbient.Intensity, dusk.Ambient.Intensity, precision: 4);
        Assert.Equal(settings.TwilightAmbient.Color, dusk.Ambient.Color);
    }

    [Fact]
    public void Evaluate_ExposureIsHigherAtNight()
    {
        var settings = DayNightCycleSettings.Default;

        var noon = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);
        var midnight = DayNightCycle.Evaluate(At(TimeOfDay.Midnight), settings);

        Assert.Equal(settings.DayExposure, noon.Exposure, precision: 4);
        Assert.Equal(settings.NightExposure, midnight.Exposure, precision: 4);
    }

    // ── Degenerate settings ──────────────────────────────────────────────────

    [Fact]
    public void Evaluate_InvertedElevationBand_StillProducesUsableLighting()
    {
        var settings = DayNightCycleSettings.Default with
        {
            DaylightElevation = -0.2f,
            NightElevation = 0.25f,
        };

        var noon = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);
        var midnight = DayNightCycle.Evaluate(At(TimeOfDay.Midnight), settings);

        Assert.Equal(1f, noon.DaylightFactor, precision: 4);
        Assert.Equal(0f, midnight.DaylightFactor, precision: 4);
    }

    [Fact]
    public void Evaluate_NegativeIntensities_AreClampedToZero()
    {
        var settings = DayNightCycleSettings.Default with
        {
            SunIntensity = -5f,
            DayAmbient = new AmbientLight { Color = Color.White, Intensity = float.NaN },
        };

        var lighting = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);

        Assert.Equal(0f, lighting.KeyLight.Intensity);
        Assert.Equal(0f, lighting.Ambient.Intensity);
    }

    [Fact]
    public void Evaluate_ZeroWidthElevationBand_DoesNotDivideByZero()
    {
        var settings = DayNightCycleSettings.Default with
        {
            DaylightElevation = 0f,
            NightElevation = 0f,
        };

        var noon = DayNightCycle.Evaluate(At(TimeOfDay.Noon), settings);
        var midnight = DayNightCycle.Evaluate(At(TimeOfDay.Midnight), settings);

        Assert.True(float.IsFinite(noon.KeyLight.Intensity));
        Assert.True(float.IsFinite(midnight.KeyLight.Intensity));
        Assert.Equal(1f, noon.DaylightFactor);
        Assert.Equal(0f, midnight.DaylightFactor);
    }

    [Fact]
    public void SunDirection_MatchesDirectionalLightConvention()
    {
        // DirectionalLight.Direction points from the fragment towards the light, so a noon sun
        // must point up (+Y), not down along its own travel.
        var lighting = DayNightCycle.Evaluate(At(TimeOfDay.Noon), DayNightCycleSettings.Default);

        Assert.True(Vector3.Dot(lighting.KeyLight.Direction, Vector3.UnitY) > 0.9f);
    }
}
