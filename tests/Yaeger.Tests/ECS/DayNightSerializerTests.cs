using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

/// <summary>
/// Round-trip and deserialization tests for <see cref="AmbientLightSerializer"/> and
/// <see cref="TimeOfDaySerializer"/>.
/// </summary>
public class DayNightSerializerTests
{
    // ── AmbientLight ─────────────────────────────────────────────────────────

    [Fact]
    public void AmbientLight_ShouldRoundTrip()
    {
        var original = new AmbientLight
        {
            Color = new Color(120, 140, 200, 255),
            Intensity = 0.42f,
        };

        var reloaded = RoundTrip(original, "ambient");

        Assert.Equal(original.Color, reloaded.Color);
        Assert.Equal(original.Intensity, reloaded.Intensity, precision: 5);
    }

    [Fact]
    public void AmbientLight_MissingProperties_ShouldUseDefaults()
    {
        var component = Deserialize<AmbientLight>("""{ "type": "AmbientLight" }""");

        Assert.Equal(AmbientLight.Default, component);
    }

    [Fact]
    public void AmbientLight_PartialJson_ShouldKeepDefaultsForAbsentProperties()
    {
        var component = Deserialize<AmbientLight>(
            """{ "type": "AmbientLight", "intensity": 0.5 }"""
        );

        Assert.Equal(0.5f, component.Intensity, precision: 5);
        Assert.Equal(AmbientLight.Default.Color, component.Color);
    }

    [Fact]
    public void AmbientLightSerializer_ComponentType_ReturnsAmbientLightType()
    {
        Assert.Equal(typeof(AmbientLight), new AmbientLightSerializer().ComponentType);
    }

    // ── TimeOfDay ────────────────────────────────────────────────────────────

    [Fact]
    public void TimeOfDay_ShouldRoundTrip()
    {
        var original = new TimeOfDay
        {
            NormalizedTime = 0.63f,
            DayLengthSeconds = 300f,
            NorthOffset = 1.1f,
            AxisTilt = 0.4f,
        };

        var reloaded = RoundTrip(original, "clock");

        Assert.Equal(original.NormalizedTime, reloaded.NormalizedTime, precision: 5);
        Assert.Equal(original.DayLengthSeconds, reloaded.DayLengthSeconds, precision: 5);
        Assert.Equal(original.NorthOffset, reloaded.NorthOffset, precision: 5);
        Assert.Equal(original.AxisTilt, reloaded.AxisTilt, precision: 5);
    }

    [Fact]
    public void TimeOfDay_MissingProperties_ShouldUseDefaults()
    {
        var component = Deserialize<TimeOfDay>("""{ "type": "TimeOfDay" }""");

        Assert.Equal(TimeOfDay.Default, component);
    }

    [Fact]
    public void TimeOfDay_PartialJson_ShouldKeepDefaultsForAbsentProperties()
    {
        var component = Deserialize<TimeOfDay>(
            """{ "type": "TimeOfDay", "normalizedTime": 0.25 }"""
        );

        Assert.Equal(0.25f, component.NormalizedTime, precision: 5);
        Assert.Equal(TimeOfDay.Default.DayLengthSeconds, component.DayLengthSeconds);
        Assert.Equal(TimeOfDay.Default.AxisTilt, component.AxisTilt);
    }

    [Fact]
    public void TimeOfDaySerializer_ComponentType_ReturnsTimeOfDayType()
    {
        Assert.Equal(typeof(TimeOfDay), new TimeOfDaySerializer().ComponentType);
    }

    // ── Helpers (mirroring Serializer3DTests) ────────────────────────────────

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
