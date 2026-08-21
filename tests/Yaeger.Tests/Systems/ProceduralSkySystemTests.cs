using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

/// <summary>
/// Tests for <see cref="ProceduralSkySystem"/>: advancing <see cref="ProceduralSky.Elapsed"/>,
/// leaving every other field untouched, and the invalid-deltaTime guard shared with
/// <see cref="DayNightCycleSystem"/>/<see cref="LightFlickerSystem"/>.
/// </summary>
public class ProceduralSkySystemTests
{
    [Fact]
    public void Update_AdvancesElapsedByDeltaTime()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, ProceduralSky.Default);
        var system = new ProceduralSkySystem(world);

        system.Update(0.4f);
        system.Update(0.4f);

        Assert.True(world.TryGetComponent<ProceduralSky>(entity, out var sky));
        Assert.Equal(0.8f, sky.Elapsed, precision: 4);
    }

    [Fact]
    public void Update_LeavesEveryOtherFieldUntouched()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var original = ProceduralSky.Default;
        world.AddComponent(entity, original);
        var system = new ProceduralSkySystem(world);

        system.Update(0.1f);

        Assert.True(world.TryGetComponent<ProceduralSky>(entity, out var sky));
        Assert.Equal(original.SunDirection, sky.SunDirection);
        Assert.Equal(original.MoonDirection, sky.MoonDirection);
        Assert.Equal(original.DaylightFactor, sky.DaylightFactor);
        Assert.Equal(original.CloudWind, sky.CloudWind);
        Assert.Equal(original.CloudScale, sky.CloudScale);
        Assert.Equal(original.CloudCoverage, sky.CloudCoverage);
        Assert.Equal(original.StarDensity, sky.StarDensity);
        Assert.Equal(original.MoonPhase, sky.MoonPhase);
    }

    [Fact]
    public void Update_MultipleSkies_EachAdvancesIndependently()
    {
        var world = new World();
        var a = world.CreateEntity();
        world.AddComponent(a, ProceduralSky.Default);
        var b = world.CreateEntity();
        world.AddComponent(b, ProceduralSky.Default with { Elapsed = 5f });
        var system = new ProceduralSkySystem(world);

        system.Update(1f);

        Assert.True(world.TryGetComponent<ProceduralSky>(a, out var skyA));
        Assert.True(world.TryGetComponent<ProceduralSky>(b, out var skyB));
        Assert.Equal(1f, skyA.Elapsed, precision: 4);
        Assert.Equal(6f, skyB.Elapsed, precision: 4);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Update_InvalidDeltaTime_IsIgnored(float deltaTime)
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, ProceduralSky.Default);
        var system = new ProceduralSkySystem(world);

        system.Update(deltaTime);

        Assert.True(world.TryGetComponent<ProceduralSky>(entity, out var sky));
        Assert.Equal(0f, sky.Elapsed);
    }

    [Fact]
    public void Update_WithoutAnyProceduralSky_DoesNotThrow()
    {
        var world = new World();
        world.CreateEntity();
        var system = new ProceduralSkySystem(world);

        var exception = Record.Exception(() => system.Update(0.1f));

        Assert.Null(exception);
    }
}
