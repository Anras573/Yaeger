using System.Text.Json;
using Yaeger.Assets.HotReload;
using Yaeger.ECS;

namespace Yaeger.Tests.Assets.HotReload;

public class SceneHotReloadTests
{
    [Fact]
    public void Reload_ValidFile_RaisesReloadedWithParsedScene()
    {
        var json = "{ \"entities\": [ { \"components\": [ { \"type\": \"Stub\" } ] } ] }";
        var path = WriteTempScene(json);

        try
        {
            var hotReload = new SceneHotReload(MakeLoader(), path);
            Scene? received = null;
            hotReload.Reloaded += scene => received = scene;

            hotReload.Reload();

            Assert.NotNull(received);
            Assert.Equal(1, received.EntityCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reload_TruncatedMidWriteFile_DoesNotRaiseReloadedOrThrow()
    {
        // Simulates catching the file mid-save: valid JSON prefix, no closing braces.
        var json = "{ \"entities\": [ { \"components\": [ { \"type\"";
        var path = WriteTempScene(json);

        try
        {
            var hotReload = new SceneHotReload(MakeLoader(), path);
            var raised = false;
            hotReload.Reloaded += _ => raised = true;

            var exception = Record.Exception(() => hotReload.Reload());

            Assert.Null(exception);
            Assert.False(raised);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reload_MissingFile_DoesNotRaiseReloadedOrThrow()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.scene.json");
        var hotReload = new SceneHotReload(MakeLoader(), missingPath);
        var raised = false;
        hotReload.Reloaded += _ => raised = true;

        var exception = Record.Exception(() => hotReload.Reload());

        Assert.Null(exception);
        Assert.False(raised);
    }

    [Fact]
    public void Path_ReturnsConstructorValue()
    {
        var hotReload = new SceneHotReload(MakeLoader(), "Scenes/level1.json");

        Assert.Equal("Scenes/level1.json", hotReload.Path);
    }

    private static string WriteTempScene(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        return path;
    }

    private static SceneLoader MakeLoader()
    {
        var registry = new ComponentRegistry();
        registry.Register(new StubSerializer("Stub"));
        return new SceneLoader(registry);
    }

    private sealed class StubSerializer(string typeId) : IComponentSerializer
    {
        public string TypeId { get; } = typeId;

        public Action<World, Entity> Deserialize(JsonElement element) => (_, _) => { };
    }
}
