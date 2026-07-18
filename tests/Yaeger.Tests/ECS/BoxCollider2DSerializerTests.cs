using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class BoxCollider2DSerializerTests
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
                  "type": "BoxCollider2D",
                  "size": [2.0, 3.0],
                  "offset": [1.0, -1.0],
                  "layer": 4,
                  "collidesWith": 6,
                  "isTrigger": true
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<BoxCollider2D>(entity, out var collider));
        Assert.Equal(new Vector2(2, 3), collider.Size);
        Assert.Equal(new Vector2(1, -1), collider.Offset);
        Assert.Equal(4, collider.Layer);
        Assert.Equal(6u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "BoxCollider2D", "size": [1.0, 1.0] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<BoxCollider2D>(entity, out var collider));
        Assert.Equal(Vector2.Zero, collider.Offset);
        Assert.Equal(0, collider.Layer);
        Assert.Equal(BoxCollider2D.AllLayers, collider.CollidesWith);
        Assert.False(collider.IsTrigger);
    }

    [Fact]
    public void MissingSize_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "BoxCollider2D" } ] }""")
        );
    }

    [Fact]
    public void InvalidLayer_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "BoxCollider2D", "size": [1, 1], "layer": 99 } ] }"""
            )
        );
    }

    [Fact]
    public void NonBooleanIsTrigger_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "BoxCollider2D", "size": [1, 1], "isTrigger": "yes" } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_BoxCollider2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("hazard");
        world.AddComponent(
            entity,
            new BoxCollider2D(
                new Vector2(2, 1),
                new Vector2(0.5f, 0),
                layer: 3,
                collidesWith: 5,
                isTrigger: true
            )
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("hazard", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<BoxCollider2D>(reloadedEntity, out var collider));
        Assert.Equal(new Vector2(2, 1), collider.Size);
        Assert.Equal(new Vector2(0.5f, 0), collider.Offset);
        Assert.Equal(3, collider.Layer);
        Assert.Equal(5u, collider.CollidesWith);
        Assert.True(collider.IsTrigger);
    }

    [Fact]
    public void SceneSaver_DefaultValuedCollider_ShouldOmitOptionalProperties()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("wall");
        world.AddComponent(entity, new BoxCollider2D(new Vector2(1, 1)));

        var json = new SceneSaver(registry).Serialize(world);

        Assert.DoesNotContain("\"offset\"", json);
        Assert.DoesNotContain("\"layer\"", json);
        Assert.DoesNotContain("\"collidesWith\"", json);
        Assert.DoesNotContain("\"isTrigger\"", json);
        Assert.DoesNotContain("\"oneWay\"", json);
        Assert.DoesNotContain("\"surfaceDirection\"", json);
    }

    [Fact]
    public void Deserializes_OneWayAndSurfaceDirection_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "BoxCollider2D",
                  "size": [2.0, 0.5],
                  "oneWay": true,
                  "surfaceDirection": [0.0, 2.0]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<BoxCollider2D>(entity, out var collider));
        Assert.True(collider.OneWay);
        Assert.Equal(Vector2.UnitY, collider.SurfaceDirection); // normalized from (0, 2)
    }

    [Fact]
    public void MissingOneWayAndSurfaceDirection_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "BoxCollider2D", "size": [1.0, 1.0] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<BoxCollider2D>(entity, out var collider));
        Assert.False(collider.OneWay);
        Assert.Equal(Vector2.UnitY, collider.SurfaceDirection);
    }

    [Fact]
    public void ZeroLengthSurfaceDirection_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                { "components": [ { "type": "BoxCollider2D", "size": [1, 1], "surfaceDirection": [0, 0] } ] }
                """
            )
        );
    }

    [Fact]
    public void SceneSaver_OneWayPlatform_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("platform");
        world.AddComponent(
            entity,
            new BoxCollider2D(
                new Vector2(3, 0.5f),
                oneWay: true,
                surfaceDirection: new Vector2(1, 0)
            )
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("platform", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<BoxCollider2D>(reloadedEntity, out var collider));
        Assert.True(collider.OneWay);
        Assert.Equal(Vector2.UnitX, collider.SurfaceDirection);
    }

    [Fact]
    public void SceneSaver_OneWayWithDefaultUpSurfaceDirection_ShouldOmitSurfaceDirection()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("cloud");
        world.AddComponent(entity, new BoxCollider2D(new Vector2(2, 0.5f), oneWay: true));

        var json = new SceneSaver(registry).Serialize(world);

        Assert.Contains("\"oneWay\"", json);
        Assert.DoesNotContain("\"surfaceDirection\"", json);
    }
}
