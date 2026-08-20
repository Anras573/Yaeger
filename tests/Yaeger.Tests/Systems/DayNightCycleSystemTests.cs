using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

/// <summary>
/// Tests for <see cref="DayNightCycleSystem"/>: advancing the clock, applying the evaluated
/// lighting to the cycle's entity, and the freeze/scrub behaviour.
/// </summary>
public class DayNightCycleSystemTests
{
    private static (World World, Entity Entity) WorldWithCycle(
        float normalizedTime = TimeOfDay.Noon,
        float dayLengthSeconds = 100f
    )
    {
        var world = new World();
        var entity = world.CreateEntity("sun");
        world.AddComponent(
            entity,
            new TimeOfDay
            {
                NormalizedTime = normalizedTime,
                DayLengthSeconds = dayLengthSeconds,
                AxisTilt = 0.35f,
            }
        );
        return (world, entity);
    }

    [Fact]
    public void Update_AdvancesNormalizedTimeByTheFractionOfADayElapsed()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0f, dayLengthSeconds: 100f);
        var system = new DayNightCycleSystem(world);

        system.Update(25f);

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.Equal(0.25f, time.NormalizedTime, precision: 5);
    }

    [Fact]
    public void Update_PastTheEndOfADay_WrapsInsteadOfDrifting()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0.9f, dayLengthSeconds: 10f);
        var system = new DayNightCycleSystem(world);

        system.Update(15f); // 1.5 days' worth

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.InRange(time.NormalizedTime, 0f, 1f);
        Assert.Equal(0.4f, time.NormalizedTime, precision: 4);
    }

    [Fact]
    public void Update_WritesTheKeyLightAndAmbientOntoTheCycleEntity()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var light));
        Assert.True(world.TryGetComponent<AmbientLight>(entity, out var ambient));
        Assert.Equal(system.CurrentLighting.KeyLight, light);
        Assert.Equal(system.CurrentLighting.Ambient, ambient);
        Assert.True(light.Intensity > 0f);
    }

    [Fact]
    public void Update_ReplacesLightComponentsTheEntityAlreadyCarried()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        world.AddComponent(
            entity,
            new DirectionalLight { Direction = -Vector3.UnitY, Intensity = 99f }
        );
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var light));
        Assert.NotEqual(99f, light.Intensity);
        Assert.True(light.Direction.Y > 0f);
    }

    [Fact]
    public void Update_ZeroDelta_AppliesTheCurrentTimeWithoutAdvancingIt()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0.3f);
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.Equal(0.3f, time.NormalizedTime, precision: 5);
        Assert.True(world.TryGetComponent<AmbientLight>(entity, out _));
    }

    [Fact]
    public void Update_ScrubbingIsDeterministic_RegardlessOfFrameHistory()
    {
        var (worldA, entityA) = WorldWithCycle(normalizedTime: 0f);
        var systemA = new DayNightCycleSystem(worldA);
        for (var i = 0; i < 40; i++)
            systemA.Update(1f); // 40 s of a 100 s day, in 1 s steps

        var (worldB, entityB) = WorldWithCycle(normalizedTime: 0.4f);
        var systemB = new DayNightCycleSystem(worldB);
        systemB.Update(0f); // scrubbed straight there

        Assert.True(worldA.TryGetComponent<TimeOfDay>(entityA, out var timeA));
        Assert.True(worldB.TryGetComponent<TimeOfDay>(entityB, out var timeB));
        Assert.Equal(timeB.NormalizedTime, timeA.NormalizedTime, precision: 4);
        Assert.Equal(
            systemB.CurrentLighting.KeyLight.Intensity,
            systemA.CurrentLighting.KeyLight.Intensity,
            precision: 4
        );
        Assert.Equal(systemB.CurrentLighting.Ambient, systemA.CurrentLighting.Ambient);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-10f)]
    [InlineData(float.NaN)]
    public void Update_NonPositiveDayLength_FreezesTheClockButStillLightsTheScene(float dayLength)
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0.3f, dayLengthSeconds: dayLength);
        var system = new DayNightCycleSystem(world);

        system.Update(5f);

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.Equal(0.3f, time.NormalizedTime, precision: 5);
        Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var light));
        Assert.True(float.IsFinite(light.Intensity));
    }

    [Fact]
    public void Update_NegativeDelta_IsIgnored()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0.3f);
        var system = new DayNightCycleSystem(world);

        system.Update(-5f);

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.Equal(0.3f, time.NormalizedTime, precision: 5);
        Assert.False(world.TryGetComponent<DirectionalLight>(entity, out _));
    }

    [Fact]
    public void Update_NonFiniteDelta_IsIgnored()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0.3f);
        var system = new DayNightCycleSystem(world);

        system.Update(float.NaN);
        system.Update(float.PositiveInfinity);

        Assert.True(world.TryGetComponent<TimeOfDay>(entity, out var time));
        Assert.Equal(0.3f, time.NormalizedTime, precision: 5);
    }

    [Fact]
    public void Update_WithoutATimeOfDayEntity_IsANoOp()
    {
        var world = new World();
        var entity = world.CreateEntity("not-a-clock");
        var system = new DayNightCycleSystem(world);
        var before = system.CurrentLighting;

        system.Update(1f);

        Assert.False(world.TryGetComponent<DirectionalLight>(entity, out _));
        Assert.Equal(before, system.CurrentLighting);
    }

    [Fact]
    public void CurrentLighting_BeforeAnyUpdate_IsSeededFromTheDefaultClock()
    {
        var world = new World();
        var system = new DayNightCycleSystem(world);

        Assert.True(float.IsFinite(system.CurrentLighting.Exposure));
        Assert.True(system.CurrentLighting.Exposure > 0f);
        Assert.True(system.CurrentLighting.KeyLight.Intensity > 0f);
    }

    [Fact]
    public void Settings_AreUsedForEvaluationAndCanBeSwappedAtRuntime()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        var system = new DayNightCycleSystem(
            world,
            DayNightCycleSettings.Default with
            {
                SunIntensity = 7f,
            }
        );

        system.Update(0f);
        Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var bright));
        Assert.Equal(7f, bright.Intensity, precision: 4);

        system.Settings = DayNightCycleSettings.Default with { SunIntensity = 1f };
        system.Update(0f);
        Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var dim));
        Assert.Equal(1f, dim.Intensity, precision: 4);
    }

    // ── Tagged sun/moon entities ─────────────────────────────────────────────

    [Fact]
    public void Update_WithTaggedBodies_WritesEachBodyToItsOwnEntity()
    {
        var (world, _) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        var sun = world.CreateEntity("sun-light");
        world.AddComponent(sun, new CelestialLight(CelestialBody.Sun));
        var moon = world.CreateEntity("moon-light");
        world.AddComponent(moon, new CelestialLight(CelestialBody.Moon));
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(sun, out var sunLight));
        Assert.True(world.TryGetComponent<DirectionalLight>(moon, out var moonLight));
        Assert.Equal(system.CurrentLighting.Sun, sunLight);
        Assert.Equal(system.CurrentLighting.Moon, moonLight);
        // Noon: the sun carries the scene and the moon is below the horizon, so it is dark.
        Assert.True(sunLight.Intensity > 0f);
        Assert.Equal(0f, moonLight.Intensity);
    }

    [Fact]
    public void Update_WithTaggedBodies_LeavesTheClockEntitysOwnLightAlone()
    {
        var (world, clock) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        var sun = world.CreateEntity("sun-light");
        world.AddComponent(sun, new CelestialLight(CelestialBody.Sun));
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        // The key light would otherwise land here and take a directional slot the scene didn't ask
        // for — the clock entity is just a clock once bodies are tagged.
        Assert.False(world.TryGetComponent<DirectionalLight>(clock, out _));
        Assert.True(world.TryGetComponent<AmbientLight>(clock, out _));
    }

    [Fact]
    public void Update_WithTaggedBodies_KeepsBothPointingOppositeEachOther()
    {
        var (world, _) = WorldWithCycle(normalizedTime: 0.4f);
        var sun = world.CreateEntity("sun-light");
        world.AddComponent(sun, new CelestialLight(CelestialBody.Sun));
        var moon = world.CreateEntity("moon-light");
        world.AddComponent(moon, new CelestialLight(CelestialBody.Moon));
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(sun, out var sunLight));
        Assert.True(world.TryGetComponent<DirectionalLight>(moon, out var moonLight));
        Assert.Equal(-sunLight.Direction.X, moonLight.Direction.X, precision: 5);
        Assert.Equal(-sunLight.Direction.Y, moonLight.Direction.Y, precision: 5);
    }

    [Fact]
    public void Update_WithOnlyAMoonTagged_StillLeavesTheClockEntityUnlit()
    {
        var (world, clock) = WorldWithCycle(normalizedTime: TimeOfDay.Midnight);
        var moon = world.CreateEntity("moon-light");
        world.AddComponent(moon, new CelestialLight(CelestialBody.Moon));
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(moon, out var moonLight));
        Assert.True(moonLight.Intensity > 0f);
        Assert.False(world.TryGetComponent<DirectionalLight>(clock, out _));
    }

    [Fact]
    public void Update_WithoutTaggedBodies_KeepsTheSingleKeyLightBehaviour()
    {
        var (world, clock) = WorldWithCycle(normalizedTime: TimeOfDay.Noon);
        var system = new DayNightCycleSystem(world);

        system.Update(0f);

        Assert.True(world.TryGetComponent<DirectionalLight>(clock, out var light));
        Assert.Equal(system.CurrentLighting.KeyLight, light);
    }

    [Fact]
    public void Update_RunningAFullCycle_KeepsTheLightPointingSomewhereValid()
    {
        var (world, entity) = WorldWithCycle(normalizedTime: 0f, dayLengthSeconds: 10f);
        var system = new DayNightCycleSystem(world);

        for (var step = 0; step < 200; step++)
        {
            system.Update(0.1f);

            Assert.True(world.TryGetComponent<DirectionalLight>(entity, out var light));
            Assert.Equal(1f, light.Direction.Length(), precision: 4);
            Assert.True(light.Intensity >= 0f);
        }
    }
}
