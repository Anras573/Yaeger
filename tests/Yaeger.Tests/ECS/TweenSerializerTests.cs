using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class TweenSerializerTests
{
    [Fact]
    public void ComponentType_ReturnsTweenType()
    {
        var serializer = new TweenSerializer();
        Assert.Equal(typeof(Tween), serializer.ComponentType);
    }

    [Fact]
    public void Deserializes_ThroughPrefabLoader_DefaultsTargetToSelf()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                { "type": "Transform2D" },
                {
                  "type": "Tween",
                  "channel": "Transform2DPosition",
                  "from": [0, 0, 0, 0],
                  "to": [10, 0, 0, 0],
                  "duration": 2.0
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Tween>(entity, out var tween));
        Assert.Equal(entity, tween.Target);
        Assert.Equal(TweenChannel.Transform2DPosition, tween.Channel);
        Assert.Equal(new Vector4(10, 0, 0, 0), tween.To);
        Assert.Equal(2.0f, tween.Duration);
        Assert.Equal(0f, tween.Delay);
        Assert.Equal(EasingFunction.Linear, tween.Easing);
        Assert.Equal(TweenLoopMode.Once, tween.LoopMode);
    }

    [Fact]
    public void Deserializes_WithAllProperties()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "Tween",
                  "channel": "PointLightIntensity",
                  "from": [0, 0, 0, 0],
                  "to": [1, 0, 0, 0],
                  "duration": 1.5,
                  "delay": 0.25,
                  "easing": "CubicInOut",
                  "loopMode": "PingPong"
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Tween>(entity, out var tween));
        Assert.Equal(TweenChannel.PointLightIntensity, tween.Channel);
        Assert.Equal(1.5f, tween.Duration);
        Assert.Equal(0.25f, tween.Delay);
        Assert.Equal(EasingFunction.CubicInOut, tween.Easing);
        Assert.Equal(TweenLoopMode.PingPong, tween.LoopMode);
    }

    [Fact]
    public void MissingChannel_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Tween", "duration": 1.0 } ] }""")
        );
    }

    [Fact]
    public void MissingDuration_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Tween", "channel": "Transform2DPosition" } ] }"""
            )
        );
    }

    [Fact]
    public void UnknownChannel_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                { "components": [ { "type": "Tween", "channel": "NotARealChannel", "duration": 1.0 } ] }
                """
            )
        );
    }

    [Fact]
    public void Deserializes_ThroughSceneLoader_ResolvesTargetTagToOtherEntity()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new SceneLoader(registry);
        var scene = loader.Parse(
            """
            {
              "entities": [
                { "tag": "light", "components": [ { "type": "PointLight" } ] },
                {
                  "tag": "carrier",
                  "components": [
                    {
                      "type": "Tween",
                      "targetTag": "light",
                      "channel": "PointLightIntensity",
                      "to": [1, 0, 0, 0],
                      "duration": 1.0
                    }
                  ]
                }
              ]
            }
            """
        );

        var world = new World();
        world.Instantiate(scene);

        Assert.True(world.TryGetEntity("light", out var light));
        Assert.True(world.TryGetEntity("carrier", out var carrier));
        Assert.True(world.TryGetComponent<Tween>(carrier, out var tween));
        Assert.Equal(light, tween.Target);
    }

    [Fact]
    public void UnknownTargetTag_ThrowsPrefabLoadExceptionOnInstantiate()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "Tween",
                  "targetTag": "nonexistent",
                  "channel": "Transform2DPosition",
                  "duration": 1.0
                }
              ]
            }
            """
        );

        var world = new World();

        Assert.Throws<PrefabLoadException>(() => world.Instantiate(prefab));
    }

    [Fact]
    public void SceneSaver_SelfTargetedTween_ShouldRoundTripWithoutTargetTag()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("box");
        world.AddComponent(
            entity,
            new Tween(
                entity,
                TweenChannel.Transform2DPosition,
                Vector4.Zero,
                new Vector4(10, 0, 0, 0),
                2f
            )
        );

        var json = new SceneSaver(registry).Serialize(world);
        Assert.DoesNotContain("targetTag", json);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("box", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<Tween>(reloadedEntity, out var tween));
        Assert.Equal(reloadedEntity, tween.Target);
        Assert.Equal(new Vector4(10, 0, 0, 0), tween.To);
        Assert.Equal(2f, tween.Duration);
    }

    [Fact]
    public void SceneSaver_CrossEntityTween_ShouldRoundTripTargetTag()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var light = world.CreateEntity("light");
        world.AddComponent(light, PointLight.Default);
        var carrier = world.CreateEntity("carrier");
        world.AddComponent(
            carrier,
            Tween.Create(light, TweenChannel.PointLightIntensity, 0f, 1f, duration: 1f)
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("light", out var reloadedLight));
        Assert.True(reloaded.TryGetEntity("carrier", out var reloadedCarrier));
        Assert.True(reloaded.TryGetComponent<Tween>(reloadedCarrier, out var tween));
        Assert.Equal(reloadedLight, tween.Target);
    }

    [Fact]
    public void SceneSaver_UntaggedTweenTarget_ThrowsSceneSaveException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var light = world.CreateEntity(); // no tag
        var carrier = world.CreateEntity("carrier");
        world.AddComponent(
            carrier,
            Tween.Create(light, TweenChannel.PointLightIntensity, 0f, 1f, duration: 1f)
        );

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    [Fact]
    public void TrySerialize_EntityWithoutTween_ReturnsNull()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var serializer = new TweenSerializer();

        Assert.Null(serializer.TrySerialize(world, entity));
    }

    [Fact]
    public void SceneSaver_NonDefaultOptionalFields_ShouldBeWritten()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("box");
        world.AddComponent(
            entity,
            new Tween(
                entity,
                TweenChannel.Transform2DPosition,
                Vector4.Zero,
                new Vector4(10, 0, 0, 0),
                duration: 2f,
                delay: 0.5f,
                easing: EasingFunction.BackOut,
                loopMode: TweenLoopMode.Loop
            )
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("box", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<Tween>(reloadedEntity, out var tween));
        Assert.Equal(0.5f, tween.Delay);
        Assert.Equal(EasingFunction.BackOut, tween.Easing);
        Assert.Equal(TweenLoopMode.Loop, tween.LoopMode);
    }
}
