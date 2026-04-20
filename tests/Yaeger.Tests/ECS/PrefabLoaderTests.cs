using System.Text.Json;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class PrefabLoaderTests
{
    // ── ComponentRegistry ─────────────────────────────────────────────────────

    [Fact]
    public void ComponentRegistry_Register_AddsSerializer()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Foo"));

        Assert.Contains("Foo", registry.RegisteredTypeIds);
    }

    [Fact]
    public void ComponentRegistry_Register_OverwritesExistingEntry()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Foo"));
        registry.Register(new StubSerializer("Foo")); // re-register

        Assert.Single(registry.RegisteredTypeIds, id => id == "Foo");
    }

    [Fact]
    public void ComponentRegistry_Register_NullThrowsArgumentNullException()
    {
        var registry = new ComponentRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    // ── PrefabLoader.Parse – happy paths ──────────────────────────────────────

    [Fact]
    public void Parse_EmptyComponentsArray_ReturnsValidPrefab()
    {
        var loader = MakeLoader();
        var prefab = loader.Parse("""{ "components": [] }""");

        Assert.NotNull(prefab);
        var world = new World();
        var entity = world.Instantiate(prefab);
        Assert.Contains(entity, world.Entities);
    }

    [Fact]
    public void Parse_SingleComponent_AppliedOnInstantiate()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Stub"));

        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "Stub" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<StubComponent>(entity, out _));
    }

    [Fact]
    public void Parse_MultipleComponents_AllAppliedOnInstantiate()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("A"));
        registry.Register(new StubSerializer2("B"));

        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "A" }, { "type": "B" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<StubComponent>(entity, out _));
        Assert.True(world.TryGetComponent<StubComponent2>(entity, out _));
    }

    // ── PrefabLoader.Parse – error cases ─────────────────────────────────────

    [Fact]
    public void Parse_InvalidJson_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        Assert.Throws<PrefabLoadException>(() => loader.Parse("NOT JSON"));
    }

    [Fact]
    public void Parse_MissingComponentsKey_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        var ex = Assert.Throws<PrefabLoadException>(() => loader.Parse("""{ "name": "Test" }"""));
        Assert.Contains("components", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ComponentsNotArray_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": "not-an-array" }""")
        );
    }

    [Fact]
    public void Parse_ComponentMissingTypeField_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "texturePath": "a.png" } ] }""")
        );
        Assert.Contains("type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_UnregisteredComponentType_ThrowsPrefabLoadExceptionWithTypeId()
    {
        var loader = MakeLoader();

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Unknown" } ] }""")
        );
        Assert.Contains("Unknown", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EmptyTypeString_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "" } ] }""")
        );
    }

    // ── Engine component serializers ──────────────────────────────────────────

    [Fact]
    public void SpriteSerializer_RoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png" } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.Equal("Assets/ball.png", sprite.TexturePath);
    }

    [Fact]
    public void Transform2DSerializer_RoundTrip_WithArrayVectors()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "Transform2D",
                  "position": [1.0, 2.0],
                  "rotation": 0.5,
                  "scale": [3.0, 4.0]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Transform2D>(entity, out var t));
        Assert.Equal(1f, t.Position.X);
        Assert.Equal(2f, t.Position.Y);
        Assert.Equal(0.5f, t.Rotation);
        Assert.Equal(3f, t.Scale.X);
        Assert.Equal(4f, t.Scale.Y);
    }

    [Fact]
    public void Transform2DSerializer_RoundTrip_DefaultsWhenPropertiesAbsent()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "Transform2D" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Transform2D>(entity, out var t));
        Assert.Equal(0f, t.Position.X);
        Assert.Equal(0f, t.Position.Y);
        Assert.Equal(0f, t.Rotation);
        Assert.Equal(1f, t.Scale.X);
        Assert.Equal(1f, t.Scale.Y);
    }

    [Fact]
    public void SpriteSheetSerializer_RoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "SpriteSheet",
                  "texturePath": "Assets/sheet.png",
                  "columns": 4,
                  "rows": 2,
                  "frameCount": 7
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<SpriteSheet>(entity, out var sheet));
        Assert.Equal("Assets/sheet.png", sheet.TexturePath);
        Assert.Equal(4, sheet.Columns);
        Assert.Equal(2, sheet.Rows);
        Assert.Equal(7, sheet.FrameCount);
    }

    [Fact]
    public void AnimationSerializer_RoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "Animation",
                  "loop": false,
                  "frames": [
                    { "texturePath": "Assets/f0.png", "duration": 0.1 },
                    { "texturePath": "Assets/f1.png", "duration": 0.2 }
                  ]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Animation>(entity, out var anim));
        Assert.False(anim.Loop);
        Assert.Equal(2, anim.Frames.Length);
        Assert.Equal("Assets/f0.png", anim.Frames[0].TexturePath);
        Assert.Equal(0.1f, anim.Frames[0].Duration, precision: 5);
        Assert.Equal("Assets/f1.png", anim.Frames[1].TexturePath);
        Assert.Equal(0.2f, anim.Frames[1].Duration, precision: 5);
    }

    [Fact]
    public void AnimationSerializer_DefaultsLoopToTrue()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "Animation",
                  "frames": [ { "texturePath": "Assets/f0.png", "duration": 0.1 } ]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Animation>(entity, out var anim));
        Assert.True(anim.Loop);
    }

    [Fact]
    public void AnimationSerializer_EmptyFrames_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Animation", "frames": [] } ] }""")
        );
    }

    [Fact]
    public void AnimationStateSerializer_RoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "AnimationState",
                  "currentFrameIndex": 2,
                  "elapsedTime": 0.05,
                  "isFinished": true
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(2, state.CurrentFrameIndex);
        Assert.Equal(0.05f, state.ElapsedTime, precision: 5);
        Assert.True(state.IsFinished);
    }

    [Fact]
    public void AnimationStateSerializer_DefaultsWhenPropertiesAbsent()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse("""{ "components": [ { "type": "AnimationState" } ] }""");

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<AnimationState>(entity, out var state));
        Assert.Equal(0, state.CurrentFrameIndex);
        Assert.Equal(0f, state.ElapsedTime);
        Assert.False(state.IsFinished);
    }

    // ── RegisterEngineComponents convenience extension ────────────────────────

    [Fact]
    public void RegisterEngineComponents_RegistersAllFiveBuiltInTypes()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();

        Assert.Contains("Sprite", registry.RegisteredTypeIds);
        Assert.Contains("Transform2D", registry.RegisteredTypeIds);
        Assert.Contains("SpriteSheet", registry.RegisteredTypeIds);
        Assert.Contains("Animation", registry.RegisteredTypeIds);
        Assert.Contains("AnimationState", registry.RegisteredTypeIds);
    }

    // ── PrefabLoader.Load – file-not-found ────────────────────────────────────

    [Fact]
    public void Load_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        var loader = MakeLoader();

        Assert.Throws<FileNotFoundException>(() => loader.Load("/nonexistent/prefab.json"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PrefabLoader MakeLoader()
    {
        var registry = new ComponentRegistry();
        return new PrefabLoader(registry);
    }

    // Minimal stub serializer that adds a StubComponent to the entity.
    private sealed class StubSerializer(string typeId) : IComponentSerializer
    {
        public string TypeId => typeId;

        public Action<World, Entity> Deserialize(JsonElement element) =>
            (world, entity) => world.AddComponent(entity, new StubComponent());
    }

    private sealed class StubSerializer2(string typeId) : IComponentSerializer
    {
        public string TypeId => typeId;

        public Action<World, Entity> Deserialize(JsonElement element) =>
            (world, entity) => world.AddComponent(entity, new StubComponent2());
    }

    private struct StubComponent { }

    private struct StubComponent2 { }
}
