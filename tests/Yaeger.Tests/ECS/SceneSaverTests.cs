using System.Numerics;
using System.Text.Json;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class SceneSaverTests
{
    // ── SceneSaver.Serialize — empty world ───────────────────────────────────

    [Fact]
    public void Serialize_EmptyWorld_ShouldProduceEmptyEntitiesArray()
    {
        var saver = MakeSaver();
        var world = new World();

        var json = saver.Serialize(world);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("entities").ValueKind);
        Assert.Equal(0, doc.RootElement.GetProperty("entities").GetArrayLength());
    }

    // ── SceneSaver.Serialize — entity tags ───────────────────────────────────

    [Fact]
    public void Serialize_TaggedEntity_ShouldEmitTagField()
    {
        var saver = MakeSaver();
        var world = new World();
        world.CreateEntity("player");

        var json = saver.Serialize(world);

        var entity = ParseFirstEntity(json);
        Assert.Equal("player", entity.GetProperty("tag").GetString());
    }

    [Fact]
    public void Serialize_AnonymousEntity_ShouldOmitTagField()
    {
        var saver = MakeSaver();
        var world = new World();
        world.CreateEntity();

        var json = saver.Serialize(world);

        var entity = ParseFirstEntity(json);
        Assert.False(entity.TryGetProperty("tag", out _));
    }

    // ── SceneSaver.Serialize — component serialization ───────────────────────

    [Fact]
    public void Serialize_SpriteComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite("Assets/player.png"));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<Sprite>(reloadedEntity, out var sprite));
        Assert.Equal("Assets/player.png", sprite.TexturePath);
    }

    [Fact]
    public void Serialize_Transform2DComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(1f, 2f), 0.5f, new Vector2(3f, 4f)));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<Transform2D>(reloadedEntity, out var t));
        Assert.Equal(new Vector2(1f, 2f), t.Position);
        Assert.Equal(0.5f, t.Rotation);
        Assert.Equal(new Vector2(3f, 4f), t.Scale);
    }

    [Fact]
    public void Serialize_SpriteSheetComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new SpriteSheet("Assets/sheet.png", 4, 2, 7));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<SpriteSheet>(reloadedEntity, out var ss));
        Assert.Equal("Assets/sheet.png", ss.TexturePath);
        Assert.Equal(4, ss.Columns);
        Assert.Equal(2, ss.Rows);
        Assert.Equal(7, ss.FrameCount);
    }

    [Fact]
    public void Serialize_SpriteSheet_DefaultFrameCount_ShouldOmitFrameCountField()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        // frameCount == columns * rows (default) — should not appear in JSON
        world.AddComponent(entity, new SpriteSheet("Assets/sheet.png", 4, 2));

        var json = new SceneSaver(registry).Serialize(world);

        var component = Assert.Single(ParseFirstEntityComponents(json));
        Assert.False(component.TryGetProperty("frameCount", out _));
    }

    [Fact]
    public void Serialize_AnimationComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Animation(
                [
                    new AnimationFrame("Assets/f0.png", 0.1f),
                    new AnimationFrame("Assets/f1.png", 0.2f),
                ],
                loop: false
            )
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<Animation>(reloadedEntity, out var anim));
        Assert.False(anim.Loop);
        Assert.Equal(2, anim.Frames.Length);
        Assert.Equal("Assets/f0.png", anim.Frames[0].TexturePath);
        Assert.Equal(0.1f, anim.Frames[0].Duration);
        Assert.Equal("Assets/f1.png", anim.Frames[1].TexturePath);
        Assert.Equal(0.2f, anim.Frames[1].Duration);
    }

    [Fact]
    public void Serialize_AnimationStateComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new AnimationState(2, 0.05f, true));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<AnimationState>(reloadedEntity, out var state));
        Assert.Equal(2, state.CurrentFrameIndex);
        Assert.Equal(0.05f, state.ElapsedTime);
        Assert.True(state.IsFinished);
    }

    // ── SceneSaver.Serialize — multi-entity scene round-trip ─────────────────

    [Fact]
    public void Serialize_MultipleEntities_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();

        var player = world.CreateEntity("player");
        world.AddComponent(player, new Sprite("Assets/player.png"));
        world.AddComponent(
            player,
            new Transform2D(new Vector2(0f, 0f), 0f, new Vector2(0.1f, 0.1f))
        );

        var ground = world.CreateEntity("ground");
        world.AddComponent(ground, new Sprite("Assets/ground.png"));
        world.AddComponent(
            ground,
            new Transform2D(new Vector2(0f, -0.9f), 0f, new Vector2(2f, 0.1f))
        );

        var unnamed = world.CreateEntity();
        world.AddComponent(unnamed, new Transform2D(Vector2.Zero));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.Equal(3, reloaded.Entities.Count());

        Assert.True(reloaded.TryGetEntity("player", out var rPlayer));
        Assert.True(reloaded.TryGetComponent<Sprite>(rPlayer, out var rPlayerSprite));
        Assert.Equal("Assets/player.png", rPlayerSprite.TexturePath);

        Assert.True(reloaded.TryGetEntity("ground", out var rGround));
        Assert.True(reloaded.TryGetComponent<Transform2D>(rGround, out var rGroundT));
        Assert.Equal(new Vector2(0f, -0.9f), rGroundT.Position);
    }

    [Fact]
    public void Serialize_EntityOrder_ShouldBeSortedByEntityIdAscending()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();

        // Tags are assigned so that Id-ascending order (charlie=2, delta=3, bravo=4)
        // differs from both alphabetical and insertion order.  If Serialize stops sorting,
        // the output will be wrong regardless of how World.Entities enumerates.
        var eFirst = world.CreateEntity("alpha"); // Id=1 — will be destroyed
        var eCharlie = world.CreateEntity("charlie"); // Id=2
        var eDelta = world.CreateEntity("delta"); // Id=3
        world.DestroyEntity(eFirst);
        var eBravo = world.CreateEntity("bravo"); // Id=4 — reuses freed slot

        var json = new SceneSaver(registry).Serialize(world);

        // Expected: ascending by Id → charlie(2), delta(3), bravo(4)
        var expectedTags = new[] { (eCharlie, "charlie"), (eDelta, "delta"), (eBravo, "bravo") }
            .OrderBy(x => x.Item1.Id)
            .Select(x => x.Item2)
            .ToArray();

        using var doc = JsonDocument.Parse(json);
        var serializedTags = doc
            .RootElement.GetProperty("entities")
            .EnumerateArray()
            .Select(e => e.GetProperty("tag").GetString())
            .ToArray();

        Assert.Equal(expectedTags, serializedTags);
    }

    // ── SceneSaver.Serialize — JSON structure ────────────────────────────────

    [Fact]
    public void Serialize_ComponentEntry_ShouldIncludeTypeField()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Sprite("Assets/x.png"));

        var json = new SceneSaver(registry).Serialize(world);

        var component = Assert.Single(ParseFirstEntityComponents(json));
        Assert.Equal("Sprite", component.GetProperty("type").GetString());
    }

    [Fact]
    public void Serialize_EntityWithNoSerializableComponents_ShouldEmitEmptyComponentsArray()
    {
        var saver = MakeSaver();
        var world = new World();
        world.CreateEntity();

        var json = saver.Serialize(world);

        var components = ParseFirstEntityComponents(json);
        Assert.Empty(components);
    }

    // ── SceneSaver.Save — filesystem ─────────────────────────────────────────

    [Fact]
    public void Save_ShouldWriteJsonMatchingSerialize()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("hero");
        world.AddComponent(entity, new Sprite("Assets/hero.png"));
        var saver = new SceneSaver(registry);
        var path = Path.GetTempFileName();

        try
        {
            saver.Save(world, path);

            var written = File.ReadAllText(path);
            Assert.Equal(saver.Serialize(world), written);
        }
        finally
        {
            File.Delete(path);
            var tmp = path + ".tmp";
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
    }

    // ── SpriteSheet default-frameCount round-trip ─────────────────────────────

    [Fact]
    public void Serialize_SpriteSheet_DefaultFrameCount_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new SpriteSheet("Assets/sheet.png", 4, 2));

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<SpriteSheet>(reloadedEntity, out var ss));
        Assert.Equal(4, ss.Columns);
        Assert.Equal(2, ss.Rows);
        Assert.Equal(8, ss.FrameCount);
    }

    // ── Animation loop:true round-trip ───────────────────────────────────────

    [Fact]
    public void Serialize_AnimationLoopTrue_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Animation([new AnimationFrame("Assets/f0.png", 0.1f)], loop: true)
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        var reloadedEntity = reloaded.Entities.Single();
        Assert.True(reloaded.TryGetComponent<Animation>(reloadedEntity, out var anim));
        Assert.True(anim.Loop);
    }

    // ── World.TryGetTag after DestroyEntity ──────────────────────────────────

    [Fact]
    public void TryGetTag_AfterDestroyEntity_ShouldReturnFalse()
    {
        var world = new World();
        var entity = world.CreateEntity("hero");

        world.DestroyEntity(entity);

        Assert.False(world.TryGetTag(entity, out _));
    }

    // ── ComponentRegistry.Serializers ────────────────────────────────────────

    [Fact]
    public void Serializers_ShouldReflectRegistrationOrder()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("A"));
        registry.Register(new StubSerializer("B"));

        var list = registry.Serializers;

        Assert.Equal(2, list.Count);
        Assert.Equal("A", list[0].TypeId);
        Assert.Equal("B", list[1].TypeId);
    }

    // ── SceneSaver — argument validation ────────────────────────────────────

    [Fact]
    public void Constructor_NullRegistry_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneSaver(null!));
    }

    [Fact]
    public void Serialize_NullWorld_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => MakeSaver().Serialize(null!));
    }

    [Fact]
    public void Save_NullWorld_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => MakeSaver().Save(null!, "out.json"));
    }

    [Fact]
    public void Save_NullOrWhiteSpacePath_ShouldThrow()
    {
        var world = new World();
        Assert.Throws<ArgumentException>(() => MakeSaver().Save(world, ""));
        Assert.Throws<ArgumentException>(() => MakeSaver().Save(world, "   "));
    }

    // ── IComponentSerializer.TrySerialize — default returns null ─────────────

    [Fact]
    public void TrySerialize_DefaultImpl_ShouldReturnNull()
    {
        IComponentSerializer serializer = new StubSerializer("Stub");
        var world = new World();
        var entity = world.CreateEntity();

        var result = serializer.TrySerialize(world, entity);

        Assert.Null(result);
    }

    // ── ComponentRegistry.Serializers ────────────────────────────────────────

    [Fact]
    public void Serializers_ShouldReturnAllRegistered()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("A"));
        registry.Register(new StubSerializer("B"));

        var serializers = registry.Serializers;
        Assert.Equal(2, serializers.Count);
        Assert.Contains(serializers, s => s.TypeId == "A");
        Assert.Contains(serializers, s => s.TypeId == "B");
    }

    // ── World.TryGetTag ───────────────────────────────────────────────────────

    [Fact]
    public void TryGetTag_TaggedEntity_ShouldReturnTrue()
    {
        var world = new World();
        var entity = world.CreateEntity("hero");

        Assert.True(world.TryGetTag(entity, out var tag));
        Assert.Equal("hero", tag);
    }

    [Fact]
    public void TryGetTag_AnonymousEntity_ShouldReturnFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.False(world.TryGetTag(entity, out _));
    }

    // ── SceneSaver validation — TrySerialize contract ────────────────────────

    [Fact]
    public void Serialize_SerializerReturnsNodeWithoutTypeField_ShouldThrowSceneSaveException()
    {
        var registry = new ComponentRegistry();
        registry.Register(new MissingTypeSerializer());
        var world = new World();
        world.CreateEntity("hero");

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    [Fact]
    public void Serialize_SerializerReturnsNonObjectNode_ShouldThrowSceneSaveException()
    {
        var registry = new ComponentRegistry();
        registry.Register(new NonObjectSerializer());
        var world = new World();
        world.CreateEntity();

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    [Fact]
    public void Serialize_SerializerThrows_ShouldUseEntityIdInErrorLabel()
    {
        var throwingSerializer = new ThrowingSerializer();
        var registry = new ComponentRegistry();
        registry.Register(throwingSerializer);
        var world = new World();
        var entity = world.CreateEntity(); // anonymous — no tag

        var ex = Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
        Assert.Contains($"id={entity.Id}", ex.Message);
    }

    private sealed class ThrowingSerializer : IComponentSerializer
    {
        public string TypeId => "Thrower";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };

        public System.Text.Json.Nodes.JsonNode? TrySerialize(World world, Entity entity) =>
            throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Serialize_SerializerReturnsNodeWithNonStringTypeField_ShouldThrowSceneSaveException()
    {
        var registry = new ComponentRegistry();
        registry.Register(new NumericTypeSerializer());
        var world = new World();
        world.CreateEntity();

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    [Fact]
    public void Serialize_SerializerReturnsMismatchedTypeId_ShouldThrowSceneSaveException()
    {
        var registry = new ComponentRegistry();
        registry.Register(new MismatchedTypeIdSerializer());
        var world = new World();
        world.CreateEntity();

        Assert.Throws<SceneSaveException>(() => new SceneSaver(registry).Serialize(world));
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    private static SceneSaver MakeSaver() => new(new ComponentRegistry());

    private static JsonElement ParseFirstEntity(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("entities").EnumerateArray().First().Clone();
    }

    private static IEnumerable<JsonElement> ParseFirstEntityComponents(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc
            .RootElement.GetProperty("entities")
            .EnumerateArray()
            .First()
            .GetProperty("components")
            .EnumerateArray()
            .Select(e => e.Clone())
            .ToArray();
    }

    private sealed class StubSerializer(string typeId) : IComponentSerializer
    {
        public string TypeId { get; } = typeId;

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };
        // TrySerialize intentionally NOT overridden — uses the default (returns null)
    }

    // Serializer that returns a valid-looking node but without a "type" field.
    private sealed class MissingTypeSerializer : IComponentSerializer
    {
        public string TypeId => "MissingType";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };

        public System.Text.Json.Nodes.JsonNode? TrySerialize(World world, Entity entity) =>
            new System.Text.Json.Nodes.JsonObject { ["texturePath"] = "x.png" };
    }

    // Serializer that returns a non-object node (e.g. a plain string).
    private sealed class NonObjectSerializer : IComponentSerializer
    {
        public string TypeId => "NonObject";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };

        public System.Text.Json.Nodes.JsonNode? TrySerialize(World world, Entity entity) =>
            System.Text.Json.Nodes.JsonValue.Create("oops");
    }

    // Serializer that returns a JsonObject whose "type" field is a number, not a string.
    private sealed class NumericTypeSerializer : IComponentSerializer
    {
        public string TypeId => "NumericType";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };

        public System.Text.Json.Nodes.JsonNode? TrySerialize(World world, Entity entity) =>
            new System.Text.Json.Nodes.JsonObject { ["type"] = 123 };
    }

    // Serializer that returns a JsonObject whose "type" field doesn't match TypeId.
    private sealed class MismatchedTypeIdSerializer : IComponentSerializer
    {
        public string TypeId => "RegisteredType";

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };

        public System.Text.Json.Nodes.JsonNode? TrySerialize(World world, Entity entity) =>
            new System.Text.Json.Nodes.JsonObject { ["type"] = "WrongType" };
    }
}
