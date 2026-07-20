using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class LocalTransform2DSerializerTests
{
    [Fact]
    public void ComponentType_ReturnsLocalTransform2DType()
    {
        var serializer = new LocalTransform2DSerializer();
        Assert.Equal(typeof(LocalTransform2D), serializer.ComponentType);
    }

    [Fact]
    public void Deserializes_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                { "type": "LocalTransform2D", "position": [1.0, 2.0], "rotation": 0.5, "scale": [3.0, 4.0] }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<LocalTransform2D>(entity, out var local));
        Assert.Equal(new Vector2(1, 2), local.Position);
        Assert.Equal(0.5f, local.Rotation);
        Assert.Equal(new Vector2(3, 4), local.Scale);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "LocalTransform2D" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<LocalTransform2D>(entity, out var local));
        Assert.Equal(Vector2.Zero, local.Position);
        Assert.Equal(0f, local.Rotation);
        Assert.Equal(Vector2.One, local.Scale);
    }

    [Fact]
    public void NonNumericPosition_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "LocalTransform2D", "position": "far" } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_LocalTransform2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("turret");
        world.AddComponent(
            entity,
            new LocalTransform2D(new Vector2(3, -4), 1.5f, new Vector2(2, 2))
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("turret", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<LocalTransform2D>(reloadedEntity, out var local));
        Assert.Equal(new Vector2(3, -4), local.Position);
        Assert.Equal(1.5f, local.Rotation);
        Assert.Equal(new Vector2(2, 2), local.Scale);
    }
}
