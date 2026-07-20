using System.Numerics;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class ParticleEmitterSerializerTests
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
                  "type": "ParticleEmitter",
                  "texturePath": "Assets/particle.png",
                  "maxParticles": 64,
                  "emitRate": 10.0,
                  "particleLifetime": 2.0,
                  "emitDirection": [1.0, 0.0],
                  "spreadAngle": 1.0,
                  "initialSpeed": 3.0,
                  "startColor": [255, 0, 0, 255],
                  "endColor": [0, 0, 255, 0],
                  "startSize": 0.2,
                  "endSize": 0.05
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<ParticleEmitter>(entity, out var emitter));
        Assert.Equal("Assets/particle.png", emitter.TexturePath);
        Assert.Equal(64, emitter.MaxParticles);
        Assert.Equal(10.0f, emitter.EmitRate);
        Assert.Equal(2.0f, emitter.ParticleLifetime);
        Assert.Equal(new Vector2(1, 0), emitter.EmitDirection);
        Assert.Equal(1.0f, emitter.SpreadAngle);
        Assert.Equal(3.0f, emitter.InitialSpeed);
        Assert.Equal((byte)255, emitter.StartColor.R);
        Assert.Equal((byte)255, emitter.EndColor.B);
        Assert.Equal(0.2f, emitter.StartSize);
        Assert.Equal(0.05f, emitter.EndSize);
    }

    [Fact]
    public void MissingOptionalProperties_ShouldUseDefaults()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "ParticleEmitter", "texturePath": "Assets/p.png" } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);
        var defaults = new ParticleEmitter("Assets/p.png");

        Assert.True(world.TryGetComponent<ParticleEmitter>(entity, out var emitter));
        Assert.Equal(defaults.MaxParticles, emitter.MaxParticles);
        Assert.Equal(defaults.EmitRate, emitter.EmitRate);
        Assert.Equal(defaults.ParticleLifetime, emitter.ParticleLifetime);
        Assert.Equal(defaults.EmitDirection, emitter.EmitDirection);
        Assert.Equal(defaults.SpreadAngle, emitter.SpreadAngle);
        Assert.Equal(defaults.InitialSpeed, emitter.InitialSpeed);
        Assert.Equal(defaults.StartSize, emitter.StartSize);
        Assert.Equal(defaults.EndSize, emitter.EndSize);
    }

    [Fact]
    public void MissingTexturePath_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "ParticleEmitter" } ] }""")
        );
    }

    [Fact]
    public void NonIntegerMaxParticles_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                { "components": [ { "type": "ParticleEmitter", "texturePath": "p.png", "maxParticles": 1.5 } ] }
                """
            )
        );
    }

    [Fact]
    public void SceneSaver_ParticleEmitterComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("smoke");
        world.AddComponent(
            entity,
            new ParticleEmitter("Assets/smoke.png")
            {
                MaxParticles = 128,
                EmitRate = 20f,
                ParticleLifetime = 1.5f,
                EmitDirection = new Vector2(0, -1),
                SpreadAngle = 0.3f,
                InitialSpeed = 2f,
                StartColor = Color.Red,
                EndColor = Color.Blue,
                StartSize = 0.3f,
                EndSize = 0.01f,
            }
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("smoke", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<ParticleEmitter>(reloadedEntity, out var emitter));
        Assert.Equal("Assets/smoke.png", emitter.TexturePath);
        Assert.Equal(128, emitter.MaxParticles);
        Assert.Equal(20f, emitter.EmitRate);
        Assert.Equal(1.5f, emitter.ParticleLifetime);
        Assert.Equal(new Vector2(0, -1), emitter.EmitDirection);
        Assert.Equal(0.3f, emitter.SpreadAngle);
        Assert.Equal(2f, emitter.InitialSpeed);
        Assert.Equal((byte)255, emitter.StartColor.R);
        Assert.Equal((byte)255, emitter.EndColor.B);
        Assert.Equal(0.3f, emitter.StartSize);
        Assert.Equal(0.01f, emitter.EndSize);
    }
}
