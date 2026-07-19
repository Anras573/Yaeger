using System.Net;
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
    public void SpriteSerializer_MissingTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Sprite" } ] }""")
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_NonStringTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Sprite", "texturePath": 123 } ] }""")
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_WithTintRGB_DeserializesCorrectly()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0, 0] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.Equal("Assets/ball.png", sprite.TexturePath);
        Assert.Equal(255, sprite.Tint.R);
        Assert.Equal(0, sprite.Tint.G);
        Assert.Equal(0, sprite.Tint.B);
        Assert.Equal(255, sprite.Tint.A); // Alpha defaults to 255
    }

    [Fact]
    public void SpriteSerializer_WithTintRGBA_DeserializesCorrectly()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0, 0, 128] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.Equal(255, sprite.Tint.R);
        Assert.Equal(0, sprite.Tint.G);
        Assert.Equal(0, sprite.Tint.B);
        Assert.Equal(128, sprite.Tint.A);
    }

    [Fact]
    public void SpriteSerializer_WithoutTint_DefaultsToWhite()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png" } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.Equal(255, sprite.Tint.R);
        Assert.Equal(255, sprite.Tint.G);
        Assert.Equal(255, sprite.Tint.B);
        Assert.Equal(255, sprite.Tint.A);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintNotArray_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": "red" } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintTooFewElements_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0] } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintTooManyElements_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [255, 0, 0, 128, 64] } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintChannelBelowZero_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [-1, 0, 0] } ] }"""
            )
        );
        Assert.Contains("between 0 and 255", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintChannelAbove255_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [300, 0, 0] } ] }"""
            )
        );
        Assert.Contains("between 0 and 255", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_InvalidTintChannelNotInteger_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "tint": [1.5, 0, 0] } ] }"""
            )
        );
        Assert.Contains("between 0 and 255", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSerializer_WithFlipFlags_DeserializesCorrectly()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png", "flipX": true, "flipY": true } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.True(sprite.FlipX);
        Assert.True(sprite.FlipY);
    }

    [Fact]
    public void SpriteSerializer_WithoutFlipFlags_DefaultsToFalse()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "Sprite", "texturePath": "Assets/ball.png" } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Sprite>(entity, out var sprite));
        Assert.False(sprite.FlipX);
        Assert.False(sprite.FlipY);
    }

    [Fact]
    public void SpriteSerializer_TrySerialize_OmitsFlipFlagsWhenFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite("Assets/ball.png"));

        var node = new SpriteSerializer().TrySerialize(world, entity);

        Assert.NotNull(node);
        var obj = node!.AsObject();
        Assert.False(obj.ContainsKey("flipX"));
        Assert.False(obj.ContainsKey("flipY"));
    }

    [Fact]
    public void SpriteSerializer_TrySerialize_IncludesFlipFlagsWhenTrue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite("Assets/ball.png", flipX: true, flipY: true));

        var node = new SpriteSerializer().TrySerialize(world, entity);

        Assert.NotNull(node);
        var obj = node!.AsObject();
        Assert.True(obj["flipX"]!.GetValue<bool>());
        Assert.True(obj["flipY"]!.GetValue<bool>());
    }

    [Fact]
    public void SpriteSheetSerializer_MissingTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "SpriteSheet", "columns": 4 } ] }""")
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_MissingColumns_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png" } ] }"""
            )
        );
        Assert.Contains("columns", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_ZeroColumns_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 0 } ] }"""
            )
        );
    }

    [Fact]
    public void SpriteSheetSerializer_ZeroRows_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "rows": 0 } ] }"""
            )
        );
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
    public void AnimationSerializer_NonObjectFrameEntry_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse("""{ "components": [ { "type": "Animation", "frames": [ 42 ] } ] }""")
        );
        Assert.Contains("frame 0", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationSerializer_MissingTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Animation", "frames": [ { "duration": 0.1 } ] } ] }"""
            )
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationSerializer_NonStringTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Animation", "frames": [ { "texturePath": 123, "duration": 0.1 } ] } ] }"""
            )
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationSerializer_EmptyTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Animation", "frames": [ { "texturePath": "", "duration": 0.1 } ] } ] }"""
            )
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationSerializer_MissingDuration_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Animation", "frames": [ { "texturePath": "f.png" } ] } ] }"""
            )
        );
        Assert.Contains("duration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationSerializer_NonPositiveDuration_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Animation", "frames": [ { "texturePath": "f.png", "duration": 0.0 } ] } ] }"""
            )
        );
        Assert.Contains("duration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationStateMachineSerializer_RoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "AnimationStateMachine",
                  "currentState": "idle",
                  "restartOnReplay": true,
                  "states": {
                    "idle": { "loop": true, "frames": [ { "texturePath": "Assets/idle0.png", "duration": 0.2 } ] },
                    "jump": { "loop": false, "frames": [ { "texturePath": "Assets/jump0.png", "duration": 0.15 } ] }
                  }
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<AnimationStateMachine>(entity, out var machine));
        Assert.Equal("idle", machine.CurrentState);
        Assert.True(machine.RestartOnReplay);
        Assert.Equal(2, machine.States.Count);
        Assert.True(machine.States["idle"].Loop);
        Assert.Equal("Assets/idle0.png", machine.States["idle"].Frames[0].TexturePath);
        Assert.False(machine.States["jump"].Loop);
        Assert.Equal("Assets/jump0.png", machine.States["jump"].Frames[0].TexturePath);
    }

    [Fact]
    public void AnimationStateMachineSerializer_DefaultsRestartOnReplayToFalse()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                {
                  "type": "AnimationStateMachine",
                  "currentState": "idle",
                  "states": {
                    "idle": { "frames": [ { "texturePath": "Assets/idle0.png", "duration": 0.2 } ] }
                  }
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<AnimationStateMachine>(entity, out var machine));
        Assert.False(machine.RestartOnReplay);
    }

    [Fact]
    public void AnimationStateMachineSerializer_MissingStates_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "AnimationStateMachine", "currentState": "idle" } ] }"""
            )
        );
        Assert.Contains("states", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationStateMachineSerializer_EmptyStates_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "AnimationStateMachine", "currentState": "idle", "states": {} } ] }"""
            )
        );
        Assert.Contains("states", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationStateMachineSerializer_MissingCurrentState_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                {
                  "components": [
                    {
                      "type": "AnimationStateMachine",
                      "states": {
                        "idle": { "frames": [ { "texturePath": "Assets/idle0.png", "duration": 0.2 } ] }
                      }
                    }
                  ]
                }
                """
            )
        );
        Assert.Contains("currentState", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationStateMachineSerializer_CurrentStateNotInStates_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                {
                  "components": [
                    {
                      "type": "AnimationStateMachine",
                      "currentState": "run",
                      "states": {
                        "idle": { "frames": [ { "texturePath": "Assets/idle0.png", "duration": 0.2 } ] }
                      }
                    }
                  ]
                }
                """
            )
        );
        Assert.Contains("currentState", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnimationStateMachineSerializer_TrySerialize_OmitsRestartOnReplayWhenFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var states = new Dictionary<string, Animation>
        {
            ["idle"] = new([new AnimationFrame("Assets/idle0.png", 0.2f)]),
        };
        world.AddComponent(entity, new AnimationStateMachine(states, "idle"));

        var node = new AnimationStateMachineSerializer().TrySerialize(world, entity);

        Assert.NotNull(node);
        var obj = node!.AsObject();
        Assert.False(obj.ContainsKey("restartOnReplay"));
        Assert.Equal("idle", obj["currentState"]!.GetValue<string>());
        Assert.True(obj["states"]!.AsObject().ContainsKey("idle"));
    }

    [Fact]
    public void AnimationStateMachineSerializer_TrySerialize_IncludesRestartOnReplayWhenTrue()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var states = new Dictionary<string, Animation>
        {
            ["idle"] = new([new AnimationFrame("Assets/idle0.png", 0.2f)]),
        };
        world.AddComponent(
            entity,
            new AnimationStateMachine(states, "idle", restartOnReplay: true)
        );

        var node = new AnimationStateMachineSerializer().TrySerialize(world, entity);

        Assert.NotNull(node);
        var obj = node!.AsObject();
        Assert.True(obj["restartOnReplay"]!.GetValue<bool>());
    }

    [Fact]
    public void SpriteSheetSerializer_WhitespaceTexturePath_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "   ", "columns": 4 } ] }"""
            )
        );
        Assert.Contains("texturePath", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_FrameCountExceedsColumnsTimesRows_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 2, "rows": 2, "frameCount": 5 } ] }"""
            )
        );
        Assert.Contains("frameCount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_WithTint_RoundTrips()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "tint": [255, 128, 0, 200] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<SpriteSheet>(entity, out var sheet));
        Assert.Equal(255, sheet.Tint.R);
        Assert.Equal(128, sheet.Tint.G);
        Assert.Equal(0, sheet.Tint.B);
        Assert.Equal(200, sheet.Tint.A);
    }

    [Fact]
    public void SpriteSheetSerializer_WithRgbTint_DefaultsAlphaTo255()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "tint": [100, 150, 200] } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<SpriteSheet>(entity, out var sheet));
        Assert.Equal(100, sheet.Tint.R);
        Assert.Equal(150, sheet.Tint.G);
        Assert.Equal(200, sheet.Tint.B);
        Assert.Equal(255, sheet.Tint.A);
    }

    [Fact]
    public void SpriteSheetSerializer_WithoutTint_DefaultsToWhite()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4 } ] }"""
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<SpriteSheet>(entity, out var sheet));
        Assert.Equal(255, sheet.Tint.R);
        Assert.Equal(255, sheet.Tint.G);
        Assert.Equal(255, sheet.Tint.B);
        Assert.Equal(255, sheet.Tint.A);
    }

    [Fact]
    public void SpriteSheetSerializer_InvalidTintChannelBelowZero_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "tint": [-1, 0, 0, 255] } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_InvalidTintChannelAbove255_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "tint": [256, 0, 0, 255] } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpriteSheetSerializer_TintNotAnArray_ThrowsPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        var ex = Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "SpriteSheet", "texturePath": "sheet.png", "columns": 4, "tint": "red" } ] }"""
            )
        );
        Assert.Contains("tint", ex.Message, StringComparison.OrdinalIgnoreCase);
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
    public void RegisterEngineComponents_RegistersAllSixBuiltInTypes()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();

        Assert.Contains("Sprite", registry.RegisteredTypeIds);
        Assert.Contains("Transform2D", registry.RegisteredTypeIds);
        Assert.Contains("SpriteSheet", registry.RegisteredTypeIds);
        Assert.Contains("Animation", registry.RegisteredTypeIds);
        Assert.Contains("AnimationState", registry.RegisteredTypeIds);
        Assert.Contains("RenderLayer", registry.RegisteredTypeIds);
    }

    // ── PrefabLoader.LoadAsync – HTTP loading ─────────────────────────────────

    [Fact]
    public async Task LoadAsync_ValidUrl_ReturnsParsedPrefab()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Stub"));
        var loader = new PrefabLoader(registry);
        var json = """{ "components": [ { "type": "Stub" } ] }""";
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, json);

        var prefab = await loader.LoadAsync("http://example.com/ball.prefab.json", httpClient);

        var world = new World();
        var entity = world.Instantiate(prefab);
        Assert.True(world.TryGetComponent<StubComponent>(entity, out _));
    }

    [Fact]
    public async Task LoadAsync_NotFoundResponse_ThrowsPrefabLoadExceptionWithStatusCode()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.NotFound, "");

        var ex = await Assert.ThrowsAsync<PrefabLoadException>(() =>
            loader.LoadAsync("http://example.com/missing.prefab.json", httpClient)
        );
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_ServerError_ThrowsPrefabLoadExceptionWithStatusCode()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.InternalServerError, "");

        var ex = await Assert.ThrowsAsync<PrefabLoadException>(() =>
            loader.LoadAsync("http://example.com/error.prefab.json", httpClient)
        );
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_NetworkFailure_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeThrowingClient();

        await Assert.ThrowsAsync<PrefabLoadException>(() =>
            loader.LoadAsync("http://example.com/ball.prefab.json", httpClient)
        );
    }

    [Fact]
    public async Task LoadAsync_NullHttpClient_ThrowsArgumentNullException()
    {
        var loader = MakeLoader();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            loader.LoadAsync("http://example.com/ball.prefab.json", null!)
        );
    }

    [Fact]
    public async Task LoadAsync_EmptyUrl_ThrowsArgumentException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync("", httpClient));
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, "NOT JSON");

        await Assert.ThrowsAsync<PrefabLoadException>(() =>
            loader.LoadAsync("http://example.com/bad.prefab.json", httpClient)
        );
    }

    // ── PrefabLoader.Load – file-not-found ────────────────────────────────────

    [Fact]
    public void Load_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        var loader = MakeLoader();
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-prefab-{Guid.NewGuid():N}.json"
        );

        Assert.Throws<FileNotFoundException>(() => loader.Load(missingPath));
    }

    // ── PrefabLoader.Parse – root shape validation ────────────────────────────

    [Fact]
    public void Parse_NonObjectRoot_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        Assert.Throws<PrefabLoadException>(() => loader.Parse("""[]"""));
    }

    [Fact]
    public void Parse_NonObjectComponentEntry_ThrowsPrefabLoadException()
    {
        var loader = MakeLoader();

        Assert.Throws<PrefabLoadException>(() => loader.Parse("""{ "components": [ 42 ] }"""));
    }

    // ── World.Instantiate – tag validation ────────────────────────────────────

    [Fact]
    public void Instantiate_EmptyTag_ThrowsArgumentException()
    {
        var world = new World();
        var prefab = new PrefabBuilder().Build();

        Assert.Throws<ArgumentException>(() => world.Instantiate(prefab, ""));
    }

    [Fact]
    public void Instantiate_WhitespaceTag_ThrowsArgumentException()
    {
        var world = new World();
        var prefab = new PrefabBuilder().Build();

        Assert.Throws<ArgumentException>(() => world.Instantiate(prefab, "   "));
    }

    // ── World tag reuse – reverse mapping correctness ────────────────────────

    [Fact]
    public void Instantiate_ReusingTag_DestroyOldEntityDoesNotRemoveNewTagMapping()
    {
        var world = new World();
        var prefab = new PrefabBuilder().Build();

        var first = world.Instantiate(prefab, "shared");
        var second = world.Instantiate(prefab, "shared");

        world.DestroyEntity(first);

        // The tag should still point to the second entity.
        Assert.True(world.TryGetEntity("shared", out var found));
        Assert.Equal(second, found);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PrefabLoader MakeLoader()
    {
        var registry = new ComponentRegistry();
        return new PrefabLoader(registry);
    }

    private static HttpClient MakeFakeClient(HttpStatusCode statusCode, string content) =>
        new(new FakeHttpMessageHandler(statusCode, content));

    private static HttpClient MakeThrowingClient() => new(new ThrowingHttpMessageHandler());

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode) { Content = new StringContent(content) }
            );
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("Simulated network failure.");
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
