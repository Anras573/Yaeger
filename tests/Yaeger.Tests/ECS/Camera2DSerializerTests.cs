using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class Camera2DSerializerTests
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
                { "type": "Camera2D", "position": [1.0, 2.0], "zoom": 2.5, "rotation": 0.5 }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Camera2D>(entity, out var camera));
        Assert.Equal(new Vector2(1, 2), camera.Position);
        Assert.Equal(2.5f, camera.Zoom);
        Assert.Equal(0.5f, camera.Rotation);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "Camera2D" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Camera2D>(entity, out var camera));
        Assert.Equal(Vector2.Zero, camera.Position);
        Assert.Equal(1f, camera.Zoom);
        Assert.Equal(0f, camera.Rotation);
    }

    [Fact]
    public void NonNumericZoom_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Camera2D", "zoom": "big" } ] }""")
        );
    }

    [Fact]
    public void SceneSaver_Camera2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("mainCamera");
        world.AddComponent(entity, new Camera2D(new Vector2(3, -4), 1.5f, 0.25f));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("mainCamera", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<Camera2D>(reloadedEntity, out var camera));
        Assert.Equal(new Vector2(3, -4), camera.Position);
        Assert.Equal(1.5f, camera.Zoom);
        Assert.Equal(0.25f, camera.Rotation);
    }
}
