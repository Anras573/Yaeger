using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class ParallaxLayerSerializerTests
{
    [Fact]
    public void Deserializes_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "ParallaxLayer",
                  "scrollFactorX": 0.2,
                  "scrollFactorY": 0.1,
                  "basePosition": [5.0, -3.0]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<ParallaxLayer>(entity, out var layer));
        Assert.Equal(0.2f, layer.ScrollFactorX);
        Assert.Equal(0.1f, layer.ScrollFactorY);
        Assert.Equal(new Vector2(5, -3), layer.BasePosition);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "ParallaxLayer" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<ParallaxLayer>(entity, out var layer));
        Assert.Equal(0.5f, layer.ScrollFactorX);
        Assert.Equal(0f, layer.ScrollFactorY);
        Assert.Equal(Vector2.Zero, layer.BasePosition);
    }

    [Fact]
    public void NonNumericScrollFactorX_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "ParallaxLayer", "scrollFactorX": "far" } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_ParallaxLayerComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("hills");
        world.AddComponent(
            entity,
            new ParallaxLayer(0.6f, 0.15f) { BasePosition = new Vector2(1, 2) }
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("hills", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<ParallaxLayer>(reloadedEntity, out var layer));
        Assert.Equal(0.6f, layer.ScrollFactorX);
        Assert.Equal(0.15f, layer.ScrollFactorY);
        Assert.Equal(new Vector2(1, 2), layer.BasePosition);
    }
}
