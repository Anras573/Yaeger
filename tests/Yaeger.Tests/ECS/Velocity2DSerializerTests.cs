using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class Velocity2DSerializerTests
{
    [Fact]
    public void Deserializes_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            { "components": [ { "type": "Velocity2D", "linear": [1.5, -2.0], "angular": 3.0 } ] }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Velocity2D>(entity, out var velocity));
        Assert.Equal(new Vector2(1.5f, -2.0f), velocity.Linear);
        Assert.Equal(3.0f, velocity.Angular);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "Velocity2D" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Velocity2D>(entity, out var velocity));
        Assert.Equal(Vector2.Zero, velocity.Linear);
        Assert.Equal(0f, velocity.Angular);
    }

    [Fact]
    public void NonNumericAngular_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Velocity2D", "angular": "fast" } ] }""")
        );
    }

    [Fact]
    public void SceneSaver_Velocity2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("projectile");
        world.AddComponent(entity, new Velocity2D(new Vector2(4, -1), 6f));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("projectile", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<Velocity2D>(reloadedEntity, out var velocity));
        Assert.Equal(new Vector2(4, -1), velocity.Linear);
        Assert.Equal(6f, velocity.Angular);
    }
}
