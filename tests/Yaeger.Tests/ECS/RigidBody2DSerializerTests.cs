using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Physics.Components;

namespace Yaeger.Tests.ECS;

public class RigidBody2DSerializerTests
{
    [Fact]
    public void Deserializes_Dynamic_ThroughPrefabLoader()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "RigidBody2D",
                  "bodyType": "Dynamic",
                  "mass": 2.0,
                  "gravityScale": 0.5,
                  "linearDrag": 0.1
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<RigidBody2D>(entity, out var body));
        Assert.Equal(BodyType.Dynamic, body.Type);
        Assert.Equal(2.0f, body.Mass);
        Assert.Equal(0.5f, body.InverseMass);
        Assert.Equal(0.5f, body.GravityScale);
        Assert.Equal(0.1f, body.LinearDrag);
    }

    [Fact]
    public void Deserializes_Dynamic_MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "RigidBody2D", "bodyType": "Dynamic", "mass": 1.0 } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<RigidBody2D>(entity, out var body));
        Assert.Equal(1.0f, body.GravityScale);
        Assert.Equal(0.0f, body.LinearDrag);
    }

    [Theory]
    [InlineData("Static")]
    [InlineData("Kinematic")]
    public void Deserializes_NonDynamic_ThroughPrefabLoader(string bodyType)
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            $$"""{ "components": [ { "type": "RigidBody2D", "bodyType": "{{bodyType}}" } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<RigidBody2D>(entity, out var body));
        Assert.Equal(Enum.Parse<BodyType>(bodyType), body.Type);
        Assert.Equal(0f, body.Mass);
        Assert.Equal(0f, body.InverseMass);
        Assert.Equal(0f, body.GravityScale);
        Assert.Equal(0f, body.LinearDrag);
    }

    [Fact]
    public void MissingBodyType_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "RigidBody2D" } ] }""")
        );
    }

    [Fact]
    public void UnrecognizedBodyType_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "RigidBody2D", "bodyType": "Floaty" } ] }"""
            )
        );
    }

    [Fact]
    public void NumericBodyType_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        // "0" would otherwise satisfy Enum.TryParse and silently resolve to BodyType.Dynamic.
        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "RigidBody2D", "bodyType": "0" } ] }""")
        );
    }

    [Fact]
    public void DynamicMissingMass_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "RigidBody2D", "bodyType": "Dynamic" } ] }"""
            )
        );
    }

    [Fact]
    public void DynamicWithNonPositiveMass_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                { "components": [ { "type": "RigidBody2D", "bodyType": "Dynamic", "mass": 0 } ] }
                """
            )
        );
    }

    [Fact]
    public void SceneSaver_DynamicRigidBody2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("crate");
        world.AddComponent(entity, RigidBody2D.CreateDynamic(3.0f, 0.75f, 0.2f));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("crate", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<RigidBody2D>(reloadedEntity, out var body));
        Assert.Equal(BodyType.Dynamic, body.Type);
        Assert.Equal(3.0f, body.Mass);
        Assert.Equal(0.75f, body.GravityScale);
        Assert.Equal(0.2f, body.LinearDrag);
    }

    [Fact]
    public void SceneSaver_StaticRigidBody2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("floor");
        world.AddComponent(entity, RigidBody2D.CreateStatic());

        var json = new SceneSaver(registry).Serialize(world);

        Assert.DoesNotContain("\"mass\"", json);
        Assert.DoesNotContain("\"gravityScale\"", json);
        Assert.DoesNotContain("\"linearDrag\"", json);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("floor", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<RigidBody2D>(reloadedEntity, out var body));
        Assert.Equal(BodyType.Static, body.Type);
    }
}
