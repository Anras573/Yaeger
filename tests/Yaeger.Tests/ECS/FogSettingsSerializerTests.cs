using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

/// <summary>
/// Round-trip and deserialization tests for <see cref="FogSettingsSerializer"/>.
/// </summary>
public class FogSettingsSerializerTests
{
    [Fact]
    public void FogSettings_ShouldRoundTrip()
    {
        var original = new FogSettings
        {
            Color = new Color(200, 210, 220, 255),
            Mode = FogMode.Linear,
            Density = 0.05f,
            Start = 5f,
            End = 80f,
        };

        var reloaded = RoundTrip(original, "fog");

        Assert.Equal(original.Color, reloaded.Color);
        Assert.Equal(original.Mode, reloaded.Mode);
        Assert.Equal(original.Density, reloaded.Density, precision: 5);
        Assert.Equal(original.Start, reloaded.Start, precision: 5);
        Assert.Equal(original.End, reloaded.End, precision: 5);
    }

    [Fact]
    public void FogSettings_MissingProperties_ShouldUseDefaults()
    {
        var component = Deserialize<FogSettings>("""{ "type": "FogSettings" }""");

        Assert.Equal(FogSettings.Default, component);
    }

    [Fact]
    public void FogSettings_PartialJson_ShouldKeepDefaultsForAbsentProperties()
    {
        var component = Deserialize<FogSettings>("""{ "type": "FogSettings", "density": 0.1 }""");

        Assert.Equal(0.1f, component.Density, precision: 5);
        Assert.Equal(FogSettings.Default.Color, component.Color);
        Assert.Equal(FogSettings.Default.Mode, component.Mode);
        Assert.Equal(FogSettings.Default.Start, component.Start);
        Assert.Equal(FogSettings.Default.End, component.End);
    }

    [Fact]
    public void FogSettings_ModeIsCaseInsensitive()
    {
        var component = Deserialize<FogSettings>("""{ "type": "FogSettings", "mode": "linear" }""");

        Assert.Equal(FogMode.Linear, component.Mode);
    }

    [Fact]
    public void FogSettings_UnknownMode_ShouldFallBackToDefault()
    {
        var component = Deserialize<FogSettings>(
            """{ "type": "FogSettings", "mode": "Volumetric" }"""
        );

        Assert.Equal(FogSettings.Default.Mode, component.Mode);
    }

    [Fact]
    public void FogSettingsSerializer_ComponentType_ReturnsFogSettingsType()
    {
        Assert.Equal(typeof(FogSettings), new FogSettingsSerializer().ComponentType);
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
