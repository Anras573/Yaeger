using System.Net;
using System.Text.Json;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;

namespace Yaeger.Tests.ECS;

public class SceneLoaderTests
{
    // ── SceneLoader.Parse — happy paths ──────────────────────────────────────

    [Fact]
    public void Parse_EmptyEntitiesArray_ReturnsSceneWithNoEntities()
    {
        var loader = MakeLoader();

        var scene = loader.Parse("""{ "entities": [] }""");

        Assert.NotNull(scene);
        Assert.Equal(0, scene.EntityCount);
    }

    [Fact]
    public void Parse_SingleAnonymousEntity_Counts()
    {
        var loader = MakeLoader();

        var scene = loader.Parse(
            """
            { "entities": [ { "components": [ { "type": "Stub" } ] } ] }
            """
        );

        Assert.Equal(1, scene.EntityCount);
    }

    [Fact]
    public void Parse_SingleTaggedEntity_Counts()
    {
        var loader = MakeLoader();

        var scene = loader.Parse(
            """
            { "entities": [ { "tag": "player", "components": [ { "type": "Stub" } ] } ] }
            """
        );

        Assert.Equal(1, scene.EntityCount);
    }

    [Fact]
    public void Parse_MultipleEntities_CountsMatches()
    {
        var loader = MakeLoader();

        var scene = loader.Parse(
            """
            {
              "entities": [
                { "tag": "a", "components": [ { "type": "Stub" } ] },
                { "components": [ { "type": "Stub" } ] },
                { "tag": "c", "components": [] }
              ]
            }
            """
        );

        Assert.Equal(3, scene.EntityCount);
    }

    // ── world.Instantiate(Scene) — exercises Scene.Apply ──────────────────────

    [Fact]
    public void Instantiate_EmptyScene_CreatesNoEntities()
    {
        var loader = MakeLoader();
        var scene = loader.Parse("""{ "entities": [] }""");
        var world = new World();

        var created = world.Instantiate(scene);

        Assert.Empty(created);
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Instantiate_SingleAnonymousEntity_CreatesOneEntity()
    {
        var loader = MakeLoader();
        var scene = loader.Parse(
            """
            { "entities": [ { "components": [ { "type": "Stub" } ] } ] }
            """
        );
        var world = new World();

        var created = world.Instantiate(scene);

        Assert.Single(created);
        Assert.Contains(created[0], world.Entities);
    }

    [Fact]
    public void Instantiate_TaggedEntity_RegistersTagOnWorld()
    {
        var loader = MakeLoader();
        var scene = loader.Parse(
            """
            { "entities": [ { "tag": "player", "components": [ { "type": "Stub" } ] } ] }
            """
        );
        var world = new World();

        var created = world.Instantiate(scene);

        Assert.Single(created);
        Assert.True(world.TryGetEntity("player", out var tagged));
        Assert.Equal(created[0], tagged);
    }

    [Fact]
    public void Instantiate_MixedTaggedAndAnonymous_PreservesOrder()
    {
        var loader = MakeLoader();
        var scene = loader.Parse(
            """
            {
              "entities": [
                { "tag": "a", "components": [] },
                { "components": [] },
                { "tag": "c", "components": [] }
              ]
            }
            """
        );
        var world = new World();

        var created = world.Instantiate(scene);

        Assert.Equal(3, created.Count);
        Assert.Equal(created[0], world.GetEntity("a"));
        Assert.Equal(created[2], world.GetEntity("c"));
    }

    [Fact]
    public void Instantiate_AppliesComponentAddersInOrder()
    {
        var registry = new ComponentRegistry();
        var recorder = new RecordingSerializer("R");
        registry.Register(recorder);

        var loader = new SceneLoader(registry);
        var scene = loader.Parse(
            """
            {
              "entities": [
                { "components": [ { "type": "R", "mark": 1 }, { "type": "R", "mark": 2 } ] }
              ]
            }
            """
        );
        var world = new World();

        world.Instantiate(scene);

        Assert.Equal(new[] { 1, 2 }, recorder.Invocations);
    }

    // ── SceneLoader.Parse — error paths ──────────────────────────────────────

    [Fact]
    public void Parse_EmptyJson_Throws()
    {
        Assert.Throws<SceneLoadException>(() => MakeLoader().Parse(""));
    }

    [Fact]
    public void Parse_MalformedJson_Throws()
    {
        Assert.Throws<SceneLoadException>(() => MakeLoader().Parse("{ not valid"));
    }

    [Fact]
    public void Parse_NonObjectRoot_Throws()
    {
        Assert.Throws<SceneLoadException>(() => MakeLoader().Parse("[]"));
    }

    [Fact]
    public void Parse_MissingEntitiesArray_Throws()
    {
        Assert.Throws<SceneLoadException>(() => MakeLoader().Parse("""{ "other": [] }"""));
    }

    [Fact]
    public void Parse_EntitiesNotArray_Throws()
    {
        Assert.Throws<SceneLoadException>(() => MakeLoader().Parse("""{ "entities": {} }"""));
    }

    [Fact]
    public void Parse_EntityMissingComponents_Throws()
    {
        Assert.Throws<SceneLoadException>(() =>
            MakeLoader().Parse("""{ "entities": [ { "tag": "a" } ] }""")
        );
    }

    [Fact]
    public void Parse_UnknownComponentType_Throws()
    {
        var ex = Assert.Throws<SceneLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "entities": [ { "components": [ { "type": "Unknown" } ] } ] }
                    """
                )
        );
        Assert.Contains("Unknown", ex.Message);
    }

    [Fact]
    public void Parse_NonStringTag_Throws()
    {
        Assert.Throws<SceneLoadException>(() =>
            MakeLoader()
                .Parse(
                    """
                    { "entities": [ { "tag": 42, "components": [] } ] }
                    """
                )
        );
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => MakeLoader().Load("does/not/exist.json"));
    }

    // ── SceneLoader.LoadAsync – HTTP loading ──────────────────────────────────

    [Fact]
    public async Task LoadAsync_ValidUrl_ReturnsParsedScene()
    {
        var loader = MakeLoader();
        var json = """{ "entities": [ { "components": [ { "type": "Stub" } ] } ] }""";
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, json);

        var scene = await loader.LoadAsync("http://example.com/level1.scene.json", httpClient);

        Assert.Equal(1, scene.EntityCount);
    }

    [Fact]
    public async Task LoadAsync_NotFoundResponse_ThrowsSceneLoadExceptionWithStatusCode()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.NotFound, "");

        var ex = await Assert.ThrowsAsync<SceneLoadException>(() =>
            loader.LoadAsync("http://example.com/missing.scene.json", httpClient)
        );
        Assert.Contains("404", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_ServerError_ThrowsSceneLoadExceptionWithStatusCode()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.InternalServerError, "");

        var ex = await Assert.ThrowsAsync<SceneLoadException>(() =>
            loader.LoadAsync("http://example.com/error.scene.json", httpClient)
        );
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_NetworkFailure_ThrowsSceneLoadException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeThrowingClient();

        await Assert.ThrowsAsync<SceneLoadException>(() =>
            loader.LoadAsync("http://example.com/level1.scene.json", httpClient)
        );
    }

    [Fact]
    public async Task LoadAsync_NullHttpClient_ThrowsArgumentNullException()
    {
        var loader = MakeLoader();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            loader.LoadAsync("http://example.com/level1.scene.json", null!)
        );
    }

    [Fact]
    public async Task LoadAsync_EmptyUrl_ThrowsArgumentException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            loader.LoadAsync("", httpClient)
        );
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ThrowsSceneLoadException()
    {
        var loader = MakeLoader();
        using var httpClient = MakeFakeClient(HttpStatusCode.OK, "NOT JSON");

        await Assert.ThrowsAsync<SceneLoadException>(() =>
            loader.LoadAsync("http://example.com/bad.scene.json", httpClient)
        );
    }

    // ── Test helpers ─────────────────────────────────────────────────────────

    private static SceneLoader MakeLoader()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Stub"));
        return new SceneLoader(registry);
    }

    private static HttpClient MakeFakeClient(HttpStatusCode statusCode, string content) =>
        new(new FakeHttpMessageHandler(statusCode, content));

    private static HttpClient MakeThrowingClient() =>
        new(new ThrowingHttpMessageHandler());

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(content),
                }
            );
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("Simulated network failure.");
    }

    private sealed class StubSerializer : IComponentSerializer
    {
        public StubSerializer(string typeId)
        {
            TypeId = typeId;
        }

        public string TypeId { get; }

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };
    }

    /// <summary>
    /// Serializer that records the "mark" field in order of invocation. Lets us verify
    /// that Scene.Apply applies component adders in scene-file order.
    /// </summary>
    private sealed class RecordingSerializer : IComponentSerializer
    {
        public RecordingSerializer(string typeId)
        {
            TypeId = typeId;
        }

        public string TypeId { get; }
        public List<int> Invocations { get; } = [];

        public Action<World, Entity> Deserialize(JsonElement element)
        {
            var mark = element.GetProperty("mark").GetInt32();
            return (_, _) => Invocations.Add(mark);
        }
    }
}
