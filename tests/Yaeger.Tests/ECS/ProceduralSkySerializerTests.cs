using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

/// <summary>
/// Round-trip and deserialization tests for <see cref="ProceduralSkySerializer"/>.
/// </summary>
public class ProceduralSkySerializerTests
{
    [Fact]
    public void ProceduralSky_ShouldRoundTrip()
    {
        var original = new ProceduralSky
        {
            SunDirection = new Vector3(0.5f, 0.6f, 0.1f),
            MoonDirection = new Vector3(-0.5f, -0.6f, -0.1f),
            DaylightFactor = 0.75f,
            CloudWind = new Vector2(0.02f, -0.03f),
            CloudScale = 3.5f,
            CloudCoverage = 0.6f,
            StarDensity = 0.99f,
            MoonPhase = 0.25f,
            Elapsed = 42.5f,
        };

        var reloaded = RoundTrip(original, "sky");

        Assert.Equal(original.SunDirection, reloaded.SunDirection);
        Assert.Equal(original.MoonDirection, reloaded.MoonDirection);
        Assert.Equal(original.DaylightFactor, reloaded.DaylightFactor, precision: 5);
        Assert.Equal(original.CloudWind, reloaded.CloudWind);
        Assert.Equal(original.CloudScale, reloaded.CloudScale, precision: 5);
        Assert.Equal(original.CloudCoverage, reloaded.CloudCoverage, precision: 5);
        Assert.Equal(original.StarDensity, reloaded.StarDensity, precision: 5);
        Assert.Equal(original.MoonPhase, reloaded.MoonPhase, precision: 5);
        Assert.Equal(original.Elapsed, reloaded.Elapsed, precision: 5);
    }

    [Fact]
    public void ProceduralSky_MissingProperties_ShouldUseDefaults()
    {
        var component = Deserialize<ProceduralSky>("""{ "type": "ProceduralSky" }""");

        Assert.Equal(ProceduralSky.Default, component);
    }

    [Fact]
    public void ProceduralSky_PartialJson_ShouldKeepDefaultsForAbsentProperties()
    {
        var component = Deserialize<ProceduralSky>(
            """{ "type": "ProceduralSky", "cloudCoverage": 0.9 }"""
        );

        Assert.Equal(0.9f, component.CloudCoverage, precision: 5);
        Assert.Equal(ProceduralSky.Default.SunDirection, component.SunDirection);
        Assert.Equal(ProceduralSky.Default.CloudWind, component.CloudWind);
        Assert.Equal(ProceduralSky.Default.StarDensity, component.StarDensity);
        Assert.Equal(ProceduralSky.Default.MoonPhase, component.MoonPhase);
    }

    [Fact]
    public void ProceduralSkySerializer_ComponentType_ReturnsProceduralSkyType()
    {
        Assert.Equal(typeof(ProceduralSky), new ProceduralSkySerializer().ComponentType);
    }

    // ── Helpers (mirroring LightFlickerSerializerTests) ──────────────────────

    private static T RoundTrip<T>(T component, string tag)
        where T : struct
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity(tag);
        world.AddComponent(entity, component);

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity(tag, out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<T>(reloadedEntity, out var result));
        return result;
    }

    private static T Deserialize<T>(string componentJson)
        where T : struct
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var prefab = new PrefabLoader(registry).Parse(
            $$"""{ "components": [ {{componentJson}} ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<T>(entity, out var component));
        return component;
    }
}
