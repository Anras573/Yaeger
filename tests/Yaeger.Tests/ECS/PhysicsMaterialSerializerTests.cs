using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class PhysicsMaterialSerializerTests
{
    [Fact]
    public void Deserializes_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            { "components": [ { "type": "PhysicsMaterial", "restitution": 1.0, "friction": 0.0 } ] }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<PhysicsMaterial>(entity, out var material));
        Assert.Equal(1.0f, material.Restitution);
        Assert.Equal(0.0f, material.Friction);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "PhysicsMaterial" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<PhysicsMaterial>(entity, out var material));
        Assert.Equal(PhysicsMaterial.Default.Restitution, material.Restitution);
        Assert.Equal(PhysicsMaterial.Default.Friction, material.Friction);
    }

    [Fact]
    public void RestitutionOutOfRange_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "PhysicsMaterial", "restitution": 1.5 } ] }"""
            )
        );
    }

    [Fact]
    public void NegativeFriction_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "PhysicsMaterial", "friction": -0.1 } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_PhysicsMaterialComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("bouncyBall");
        world.AddComponent(entity, new PhysicsMaterial(0.9f, 0.1f));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("bouncyBall", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<PhysicsMaterial>(reloadedEntity, out var material));
        Assert.Equal(0.9f, material.Restitution);
        Assert.Equal(0.1f, material.Friction);
    }
}
