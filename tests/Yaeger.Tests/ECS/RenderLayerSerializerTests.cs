using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class RenderLayerSerializerTests
{
    [Fact]
    public void RenderLayerSerializer_RoundTrip_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "RenderLayer", "value": 7 } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<RenderLayer>(entity, out var renderLayer));
        Assert.Equal(7, renderLayer.Value);
    }

    [Fact]
    public void RenderLayerSerializer_MissingValue_DefaultsToZero()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "RenderLayer" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<RenderLayer>(entity, out var renderLayer));
        Assert.Equal(0, renderLayer.Value);
    }

    [Fact]
    public void RenderLayerSerializer_NonIntegerValue_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "RenderLayer", "value": 1.5 } ] }""")
        );

        Assert.Contains("RenderLayer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SceneSaver_RenderLayerComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("hud");
        world.AddComponent(entity, new RenderLayer(11));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("hud", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<RenderLayer>(reloadedEntity, out var renderLayer));
        Assert.Equal(11, renderLayer.Value);
    }
}
