using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Graphics;

namespace Yaeger.Tests.ECS;

public class TextSerializerTests
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
                  "type": "Text",
                  "content": "Score: 0",
                  "font": "Assets/Roboto-Regular.ttf",
                  "fontSize": 24,
                  "color": [10, 20, 30, 255]
                }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Text>(entity, out var text));
        Assert.Equal("Score: 0", text.Content);
        Assert.Equal("Assets/Roboto-Regular.ttf", text.FontHandle.Id);
        Assert.Equal(24, text.FontSize);
        Assert.Equal((byte)10, text.Color.R);
        Assert.Equal((byte)20, text.Color.G);
        Assert.Equal((byte)30, text.Color.B);
    }

    [Fact]
    public void MissingColor_ShouldDefaultToWhite()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);
        var prefab = loader.Parse(
            """
            {
              "components": [
                { "type": "Text", "content": "Hi", "font": "Assets/Roboto-Regular.ttf", "fontSize": 12 }
              ]
            }
            """
        );

        var world = new World();
        var entity = world.Instantiate(prefab);

        Assert.True(world.TryGetComponent<Text>(entity, out var text));
        Assert.Equal((byte)255, text.Color.R);
        Assert.Equal((byte)255, text.Color.G);
        Assert.Equal((byte)255, text.Color.B);
        Assert.Equal((byte)255, text.Color.A);
    }

    [Fact]
    public void MissingContent_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Text", "font": "f.ttf", "fontSize": 10 } ] }"""
            )
        );
    }

    [Fact]
    public void MissingFont_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Text", "content": "Hi", "fontSize": 10 } ] }"""
            )
        );
    }

    [Fact]
    public void BlankFont_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """
                { "components": [ { "type": "Text", "content": "Hi", "font": "  ", "fontSize": 10 } ] }
                """
            )
        );
    }

    [Fact]
    public void MissingFontSize_ShouldThrowPrefabLoadException()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var loader = new PrefabLoader(registry);

        Assert.Throws<PrefabLoadException>(() =>
            loader.Parse(
                """{ "components": [ { "type": "Text", "content": "Hi", "font": "f.ttf" } ] }"""
            )
        );
    }

    [Fact]
    public void SceneSaver_TextComponent_ShouldRoundTrip()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("hud");
        world.AddComponent(
            entity,
            new Text("Lives: 3", new FontHandle("Assets/Roboto-Regular.ttf"), 18, Color.Red)
        );

        var json = new SceneSaver(registry).Serialize(world);

        var reloaded = new World();
        reloaded.Instantiate(new SceneLoader(registry).Parse(json));

        Assert.True(reloaded.TryGetEntity("hud", out var reloadedEntity));
        Assert.True(reloaded.TryGetComponent<Text>(reloadedEntity, out var text));
        Assert.Equal("Lives: 3", text.Content);
        Assert.Equal("Assets/Roboto-Regular.ttf", text.FontHandle.Id);
        Assert.Equal(18, text.FontSize);
        Assert.Equal((byte)255, text.Color.R);
        Assert.Equal((byte)0, text.Color.G);
        Assert.Equal((byte)0, text.Color.B);
    }

    [Fact]
    public void SceneSaver_WhiteText_ShouldOmitColor()
    {
        var registry = new ComponentRegistry().RegisterEngineComponents();
        var world = new World();
        var entity = world.CreateEntity("label");
        world.AddComponent(
            entity,
            new Text("Plain", new FontHandle("Assets/Roboto-Regular.ttf"), 14, Color.White)
        );

        var json = new SceneSaver(registry).Serialize(world);

        Assert.DoesNotContain("\"color\"", json);
    }
}
