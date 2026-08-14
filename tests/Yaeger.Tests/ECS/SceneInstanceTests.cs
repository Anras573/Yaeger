using System.Text.Json;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;
using Yaeger.Systems;

namespace Yaeger.Tests.ECS;

public class SceneInstanceTests
{
    // ── LoadScene / Unload ──────────────────────────────────────────────────

    [Fact]
    public void LoadScene_ReturnsInstanceWithEntitiesInSceneOrder()
    {
        var world = new World();
        var scene = MakeLoader()
            .Parse(
                """
                { "entities": [ { "components": [ { "type": "Stub" } ] }, { "components": [ { "type": "Stub" } ] } ] }
                """
            );

        var instance = world.LoadScene(scene);

        Assert.Equal(2, instance.Entities.Count);
        Assert.Equal(instance.Entities, world.Entities.OrderBy(e => e.Id));
        Assert.False(instance.IsUnloaded);
    }

    [Fact]
    public void Unload_DestroysExactlyThisInstancesEntities()
    {
        var world = new World();
        var scene = MakeLoader()
            .Parse("""{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }""");
        var instance = world.LoadScene(scene);
        var untouched = world.CreateEntity();

        instance.Unload();

        foreach (var entity in instance.Entities)
            Assert.DoesNotContain(entity, world.Entities);
        Assert.Contains(untouched, world.Entities);
        Assert.True(instance.IsUnloaded);
    }

    [Fact]
    public void Unload_TwoCoexistingInstancesOfTheSameScene_UnloadingOneLeavesTheOtherIntact()
    {
        var world = new World();
        var scene = MakeLoader()
            .Parse("""{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }""");

        var first = world.LoadScene(scene);
        var second = world.LoadScene(scene);

        first.Unload();

        foreach (var entity in first.Entities)
            Assert.DoesNotContain(entity, world.Entities);
        foreach (var entity in second.Entities)
            Assert.Contains(entity, world.Entities);
        Assert.True(first.IsUnloaded);
        Assert.False(second.IsUnloaded);
    }

    [Fact]
    public void Unload_CalledTwice_IsIdempotentNoOp()
    {
        var world = new World();
        var scene = MakeLoader()
            .Parse("""{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }""");
        var instance = world.LoadScene(scene);

        instance.Unload();
        var exception = Record.Exception(instance.Unload);

        Assert.Null(exception);
        Assert.True(instance.IsUnloaded);
    }

    // ── SwapScene ────────────────────────────────────────────────────────────

    [Fact]
    public void SwapScene_NullPrevious_JustLoadsNext()
    {
        var world = new World();
        var scene = MakeLoader()
            .Parse("""{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }""");

        var instance = world.SwapScene(null, scene);

        Assert.Single(instance.Entities);
        Assert.Contains(instance.Entities[0], world.Entities);
    }

    [Fact]
    public void SwapScene_UnloadsPreviousBeforeLoadingNext()
    {
        var world = new World();
        var loader = MakeLoader();
        var sceneA = loader.Parse(
            """{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }"""
        );
        var sceneB = loader.Parse(
            """{ "entities": [ { "components": [ { "type": "Stub" } ] }, { "components": [ { "type": "Stub" } ] } ] }"""
        );

        var instanceA = world.LoadScene(sceneA);
        var entityA = instanceA.Entities[0];

        var instanceB = world.SwapScene(instanceA, sceneB);

        Assert.True(instanceA.IsUnloaded);
        Assert.DoesNotContain(entityA, world.Entities);
        Assert.Equal(2, instanceB.Entities.Count);
        foreach (var entity in instanceB.Entities)
            Assert.Contains(entity, world.Entities);
    }

    [Fact]
    public void SwapScene_NextSceneReusesATagThePreviousInstanceHeld()
    {
        var world = new World();
        var loader = MakeLoader();
        var sceneA = loader.Parse(
            """{ "entities": [ { "tag": "player", "components": [ { "type": "Stub" } ] } ] }"""
        );
        var sceneB = loader.Parse(
            """{ "entities": [ { "tag": "player", "components": [ { "type": "Stub" } ] } ] }"""
        );

        var instanceA = world.LoadScene(sceneA);
        var playerA = instanceA.Entities[0];

        var instanceB = world.SwapScene(instanceA, sceneB);

        Assert.True(world.TryGetEntity("player", out var playerNow));
        Assert.Equal(instanceB.Entities[0], playerNow);
        Assert.NotEqual(playerA, playerNow);
        Assert.DoesNotContain(playerA, world.Entities);
    }

    // ── System-state cleanup on unload (issue #191 acceptance criterion) ────
    //
    // ParticleSystem and TilemapColliderSystem already diff-and-clean their per-entity state
    // against plain World.DestroyEntity (see ParticleSystemTests.Update_WhenEmitterEntityDestroyed_ShouldRemovePool
    // and TilemapColliderSystemTests.Update_DestroyedTilemapEntity_ShouldRemoveItsGeneratedColliders).
    // What's new here is proving the *new* SceneInstance.Unload() lifecycle path actually reaches
    // that same cleanup — not assuming it does because Unload() happens to call DestroyEntity.
    //
    // AudioSystem uses the identical diff-and-clean pattern (see its class remarks) but is not
    // exercised here: it requires a live OpenAL AudioContext, and per this repo's own test
    // conventions ("Anything needing a live OpenGL/audio context ... is not unit-tested") no test
    // anywhere in this suite constructs one. Its cleanup is verified by code inspection only.

    [Fact]
    public void Unload_ReleasesParticleSystemPoolForItsEmitterEntity()
    {
        var world = new World();
        var scene = MakeLoader(registerEngineComponents: true)
            .Parse(
                """
                {
                  "entities": [
                    {
                      "components": [
                        { "type": "Transform2D", "position": [0.0, 0.0] },
                        {
                          "type": "ParticleEmitter",
                          "texturePath": "Assets/particle.png",
                          "maxParticles": 4,
                          "emitRate": 100.0,
                          "particleLifetime": 10.0
                        }
                      ]
                    }
                  ]
                }
                """
            );
        var instance = world.LoadScene(scene);
        var emitter = instance.Entities[0];

        var particleSystem = new ParticleSystem(world);
        particleSystem.Update(0.1f);
        Assert.True(particleSystem.TryGetPool(emitter, out _));

        instance.Unload();
        particleSystem.Update(0.1f);

        Assert.False(particleSystem.TryGetPool(emitter, out _));
    }

    [Fact]
    public void Unload_ReleasesTilemapColliderSystemsGeneratedColliders()
    {
        var world = new World();
        var scene = MakeLoader(registerEngineComponents: true)
            .Parse(
                """
                {
                  "entities": [
                    {
                      "components": [
                        { "type": "Transform2D", "position": [0.0, 0.0] },
                        {
                          "type": "Tilemap",
                          "texturePath": "Assets/tiles.png",
                          "columns": 2,
                          "width": 2,
                          "height": 1,
                          "tiles": [0, 0],
                          "solidTiles": [0]
                        }
                      ]
                    }
                  ]
                }
                """
            );
        var instance = world.LoadScene(scene);

        var tilemapColliderSystem = new TilemapColliderSystem(world);
        tilemapColliderSystem.Update(0f);
        Assert.Single(world.Query<BoxCollider2D, Transform2D>().ToList());

        instance.Unload();
        tilemapColliderSystem.Update(0f);

        Assert.Empty(world.Query<BoxCollider2D, Transform2D>().ToList());
    }

    [Fact]
    public void Unload_OneOfTwoLoadedTilemapInstances_OnlyRemovesItsOwnGeneratedCollider()
    {
        // Isolation, but through a real per-entity system this time rather than just World
        // bookkeeping: two additively-loaded tilemap instances each generate their own collider,
        // and unloading one must not disturb the other's.
        var world = new World();
        var loader = MakeLoader(registerEngineComponents: true);
        var tilemapJson = (float x) =>
            $$"""
                {
                  "entities": [
                    {
                      "components": [
                        { "type": "Transform2D", "position": [{{x}}, 0.0] },
                        {
                          "type": "Tilemap",
                          "texturePath": "Assets/tiles.png",
                          "columns": 2,
                          "width": 2,
                          "height": 1,
                          "tiles": [0, 0],
                          "solidTiles": [0]
                        }
                      ]
                    }
                  ]
                }
                """;

        var instanceA = world.LoadScene(loader.Parse(tilemapJson(0f)));
        var instanceB = world.LoadScene(loader.Parse(tilemapJson(10f)));

        var tilemapColliderSystem = new TilemapColliderSystem(world);
        tilemapColliderSystem.Update(0f);
        Assert.Equal(2, world.Query<BoxCollider2D, Transform2D>().ToList().Count);

        instanceA.Unload();
        tilemapColliderSystem.Update(0f);

        Assert.Single(world.Query<BoxCollider2D, Transform2D>().ToList());
        foreach (var entity in instanceB.Entities)
            Assert.Contains(entity, world.Entities);
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    private static SceneLoader MakeLoader(bool registerEngineComponents = false)
    {
        var registry = new ComponentRegistry();
        if (registerEngineComponents)
            registry.RegisterEngineComponents();
        else
            registry.Register(new StubSerializer());
        return new SceneLoader(registry);
    }

    private sealed class StubSerializer : IComponentSerializer
    {
        public string TypeId => "Stub";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };
    }
}
