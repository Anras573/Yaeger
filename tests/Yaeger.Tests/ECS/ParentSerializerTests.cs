using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class ParentSerializerTests
{
    [Fact]
    public void ComponentType_ReturnsParentType()
    {
        var serializer = new ParentSerializer();
        Assert.Equal(typeof(Parent), serializer.ComponentType);
    }

    [Fact]
    public void Deserializes_ThroughSceneLoader_ResolvesTagToParentEntity()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new SceneLoader(registry);
        var scene = loader.Parse(
            """
            {
              "entities": [
                { "tag": "tank", "components": [ { "type": "Transform2D" } ] },
                { "tag": "turret", "components": [ { "type": "Parent", "parentTag": "tank" } ] }
              ]
            }
            """
        );

        var world = new World();
        world.Instantiate(scene);

        Assert.True(world.TryGetEntity("tank", out var tank));
        Assert.True(world.TryGetEntity("turret", out var turret));
        Assert.True(world.TryGetComponent<Parent>(turret, out var parent));
        Assert.Equal(tank, parent.ParentEntity);
    }

    [Fact]
    public void Deserializes_ChildListedBeforeParent_ResolvesForwardReference()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new SceneLoader(registry);
        var scene = loader.Parse(
            """
            {
              "entities": [
                { "tag": "turret", "components": [ { "type": "Parent", "parentTag": "tank" } ] },
                { "tag": "tank", "components": [ { "type": "Transform2D" } ] }
              ]
            }
            """
        );

        var world = new World();
        world.Instantiate(scene);

        Assert.True(world.TryGetEntity("tank", out var tank));
        Assert.True(world.TryGetEntity("turret", out var turret));
        Assert.True(world.TryGetComponent<Parent>(turret, out var parent));
        Assert.Equal(tank, parent.ParentEntity);
    }

    [Fact]
    public void MissingParentTag_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Parent" } ] }""")
        );
    }

    [Fact]
    public void NonStringParentTag_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Parent", "parentTag": 42 } ] }""")
        );
    }

    [Fact]
    public void UnknownParentTag_ThrowsPrefabLoadExceptionOnInstantiate()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Parent", "parentTag": "nonexistent" } ] }"""
        );

        var world = new World();

        Assert.Throws<PrefabLoadException>(() => world.Instantiate(prefab));
    }

    [Fact]
    public void SceneSaver_TaggedParent_RoundTrips()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var tank = world.CreateEntity("tank");
        world.AddComponent(tank, new Transform2D());
        var turret = world.CreateEntity("turret");
        world.AddComponent(turret, new Parent(tank));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("tank", out var reloadedTank));
        Assert.True(reloaded.TryGetEntity("turret", out var reloadedTurret));
        Assert.True(reloaded.TryGetComponent<Parent>(reloadedTurret, out var parent));
        Assert.Equal(reloadedTank, parent.ParentEntity);
    }

    [Fact]
    public void SceneSaver_UntaggedParent_ThrowsSceneSaveException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var tank = world.CreateEntity(); // no tag
        var turret = world.CreateEntity("turret");
        world.AddComponent(turret, new Parent(tank));

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    [Fact]
    public void TrySerialize_EntityWithoutParent_ReturnsNull()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var serializer = new ParentSerializer();

        Assert.Null(serializer.TrySerialize(world, entity));
    }
}
