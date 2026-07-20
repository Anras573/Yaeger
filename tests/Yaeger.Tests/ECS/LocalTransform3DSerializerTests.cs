using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class LocalTransform3DSerializerTests
{
    [Fact]
    public void ComponentType_ReturnsLocalTransform3DType()
    {
        var serializer = new LocalTransform3DSerializer();
        Assert.Equal(typeof(LocalTransform3D), serializer.ComponentType);
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
                {
                  "type": "LocalTransform3D",
                  "position": [1.0, 2.0, 3.0],
                  "rotation": [0.0, 0.0, 0.0, 1.0],
                  "scale": [2.0, 2.0, 2.0]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<LocalTransform3D>(entity, out var local));
        Assert.Equal(new Vector3(1, 2, 3), local.Position);
        Assert.Equal(Quaternion.Identity, local.Rotation);
        Assert.Equal(new Vector3(2, 2, 2), local.Scale);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseIdentityDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "LocalTransform3D" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<LocalTransform3D>(entity, out var local));
        Assert.Equal(Vector3.Zero, local.Position);
        Assert.Equal(Quaternion.Identity, local.Rotation);
        Assert.Equal(Vector3.One, local.Scale);
    }

    [Fact]
    public void NonNumericPosition_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "LocalTransform3D", "position": "far" } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_LocalTransform3DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("weapon");
        world.AddComponent(
            entity,
            new LocalTransform3D(new Vector3(1, 2, 3), Quaternion.Identity, new Vector3(1, 1, 1))
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("weapon", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<LocalTransform3D>(reloadedEntity, out var local));
        Assert.Equal(new Vector3(1, 2, 3), local.Position);
        Assert.Equal(Quaternion.Identity, local.Rotation);
        Assert.Equal(new Vector3(1, 1, 1), local.Scale);
    }
}
