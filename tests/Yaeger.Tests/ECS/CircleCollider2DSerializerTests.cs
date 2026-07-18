using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class CircleCollider2DSerializerTests
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
                  "type": "CircleCollider2D",
                  "radius": 0.75,
                  "offset": [0.5, 0.0],
                  "layer": 2,
                  "collidesWith": 3,
                  "isTrigger": true
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<CircleCollider2D>(entity, out var collider));
        Assert.Equal(0.75f, collider.Radius);
        Assert.Equal(new Vector2(0.5f, 0), collider.Offset);
        Assert.Equal(2, collider.Layer);
        Assert.Equal(3u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "CircleCollider2D", "radius": 1.0 } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<CircleCollider2D>(entity, out var collider));
        Assert.Equal(Vector2.Zero, collider.Offset);
        Assert.Equal(0, collider.Layer);
        Assert.Equal(CircleCollider2D.AllLayers, collider.CollidesWith);
        Assert.False(collider.IsTrigger);
    }

    [Fact]
    public void MissingRadius_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "CircleCollider2D" } ] }""")
        );
    }

    [Fact]
    public void InvalidLayer_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "CircleCollider2D", "radius": 1, "layer": -1 } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_CircleCollider2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("coin");
        world.AddComponent(
            entity,
            new CircleCollider2D(
                0.5f,
                new Vector2(0, 0.25f),
                layer: 7,
                collidesWith: 9,
                isTrigger: true
            )
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("coin", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<CircleCollider2D>(reloadedEntity, out var collider));
        Assert.Equal(0.5f, collider.Radius);
        Assert.Equal(new Vector2(0, 0.25f), collider.Offset);
        Assert.Equal(7, collider.Layer);
        Assert.Equal(9u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void SceneSaver_DefaultValuedCollider_ShouldOmitOptionalProperties()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("ball");
        world.AddComponent(entity, new CircleCollider2D(1f));

        var json = new SceneSaver(registry).Serialize(world);

        Assert.DoesNotContain("\"offset\"", json);
        Assert.DoesNotContain("\"layer\"", json);
        Assert.DoesNotContain("\"collidesWith\"", json);
        Assert.DoesNotContain("\"isTrigger\"", json);
    }
}
