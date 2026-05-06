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
    public void Serialize_EmptyWorld_ProducesEmptyEntitiesArray()
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
    public void Serialize_TaggedEntity_EmitsTagField()
    {
        var saver = MakeSaver();
        var world = new World();
        world.CreateEntity("player");

        var json = saver.Serialize(world);

        var entity = ParseFirstEntity(json);
        Assert.Equal("player", entity.GetProperty("tag").GetString());
    }

    [Fact]
    public void Serialize_AnonymousEntity_OmitsTagField()
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
    public void Serialize_SpriteComponent_RoundTrips()
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
    public void Serialize_Transform2DComponent_RoundTrips()
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
    public void Serialize_SpriteSheetComponent_RoundTrips()
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
    public void Serialize_SpriteSheet_DefaultFrameCount_OmitsFrameCountField()
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
    public void Serialize_AnimationComponent_RoundTrips()
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
    public void Serialize_AnimationStateComponent_RoundTrips()
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
    public void Serialize_MultipleEntities_RoundTrip()
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
    public void Serialize_EntityOrder_IsSortedByEntityId()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        world.CreateEntity("first");
        world.CreateEntity("second");
        world.CreateEntity("third");

        var json = new SceneSaver(registry).Serialize(world);

        using var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities").EnumerateArray().ToArray();
        Assert.Equal(3, entities.Length);
        Assert.Equal("first", entities[0].GetProperty("tag").GetString());
        Assert.Equal("second", entities[1].GetProperty("tag").GetString());
        Assert.Equal("third", entities[2].GetProperty("tag").GetString());
    }

    // ── SceneSaver.Serialize — JSON structure ────────────────────────────────

    [Fact]
    public void Serialize_ComponentEntry_IncludesTypeField()
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
    public void Serialize_EntityWithNoSerializableComponents_EmitsEmptyComponentsArray()
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
    public void Save_WritesJsonMatchingSerialize_ToFile()
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
    public void Serialize_SpriteSheet_DefaultFrameCount_RoundTrips()
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
    public void Serialize_AnimationLoopTrue_RoundTrips()
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
    public void TryGetTag_AfterDestroyEntity_ReturnsFalse()
    {
        var world = new World();
        var entity = world.CreateEntity("hero");

        world.DestroyEntity(entity);

        Assert.False(world.TryGetTag(entity, out _));
    }

    // ── ComponentRegistry.Serializers snapshot immutability ──────────────────

    [Fact]
    public void Serializers_SnapshotIsImmutable_AfterSubsequentRegistration()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("A"));

        var snapshot = registry.Serializers;

        registry.Register(new StubSerializer("B"));

        Assert.Single(snapshot);
        Assert.Equal(2, registry.Serializers.Count);
    }

    // ── SceneSaver — argument validation ────────────────────────────────────

    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneSaver(null!));
    }

    [Fact]
    public void Serialize_NullWorld_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MakeSaver().Serialize(null!));
    }

    [Fact]
    public void Save_NullWorld_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MakeSaver().Save(null!, "out.json"));
    }

    [Fact]
    public void Save_NullOrWhiteSpacePath_Throws()
    {
        var world = new World();
        Assert.Throws<ArgumentException>(() => MakeSaver().Save(world, ""));
        Assert.Throws<ArgumentException>(() => MakeSaver().Save(world, "   "));
    }

    // ── IComponentSerializer.TrySerialize — default returns null ─────────────

    [Fact]
    public void TrySerialize_DefaultImpl_ReturnsNull()
    {
        IComponentSerializer serializer = new StubSerializer("Stub");
        var world = new World();
        var entity = world.CreateEntity();

        var result = serializer.TrySerialize(world, entity);

        Assert.Null(result);
    }

    // ── ComponentRegistry.Serializers ────────────────────────────────────────

    [Fact]
    public void Serializers_ReturnsAllRegistered()
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
    public void TryGetTag_TaggedEntity_ReturnsTrue()
    {
        var world = new World();
        var entity = world.CreateEntity("hero");

        Assert.True(world.TryGetTag(entity, out var tag));
        Assert.Equal("hero", tag);
    }

    [Fact]
    public void TryGetTag_AnonymousEntity_ReturnsFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.False(world.TryGetTag(entity, out _));
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
}
