using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

/// <summary>
/// Round-trip and deserialization tests for <see cref="LightFlickerSerializer"/>.
/// </summary>
public class LightFlickerSerializerTests
{
    [Fact]
    public void LightFlicker_ShouldRoundTrip()
    {
        var original = new LightFlicker
        {
            BaseIntensity = 1.5f,
            Amplitude = 0.4f,
            Frequency = 5f,
            Seed = 3.2f,
            PositionJitter = 0.1f,
            Elapsed = 12.5f,
        };

        var reloaded = RoundTrip(original, "flicker");

        Assert.Equal(original.BaseIntensity, reloaded.BaseIntensity, precision: 5);
        Assert.Equal(original.Amplitude, reloaded.Amplitude, precision: 5);
        Assert.Equal(original.Frequency, reloaded.Frequency, precision: 5);
        Assert.Equal(original.Seed, reloaded.Seed, precision: 5);
        Assert.Equal(original.PositionJitter, reloaded.PositionJitter, precision: 5);
        Assert.Equal(original.Elapsed, reloaded.Elapsed, precision: 5);
    }

    [Fact]
    public void LightFlicker_MissingProperties_ShouldUseDefaults()
    {
        var component = Deserialize<LightFlicker>("""{ "type": "LightFlicker" }""");

        Assert.Equal(LightFlicker.Default, component);
    }

    [Fact]
    public void LightFlicker_PartialJson_ShouldKeepDefaultsForAbsentProperties()
    {
        var component = Deserialize<LightFlicker>(
            """{ "type": "LightFlicker", "frequency": 7.5 }"""
        );

        Assert.Equal(7.5f, component.Frequency, precision: 5);
        Assert.Equal(LightFlicker.Default.BaseIntensity, component.BaseIntensity);
        Assert.Equal(LightFlicker.Default.Amplitude, component.Amplitude);
        Assert.Equal(LightFlicker.Default.Seed, component.Seed);
        Assert.Equal(LightFlicker.Default.PositionJitter, component.PositionJitter);
    }

    [Fact]
    public void LightFlickerSerializer_ComponentType_ReturnsLightFlickerType()
    {
        Assert.Equal(typeof(LightFlicker), new LightFlickerSerializer().ComponentType);
    }

    // ── Helpers (mirroring DayNightSerializerTests) ──────────────────────────

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
