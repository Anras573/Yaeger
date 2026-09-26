using System.Numerics;
using Yaeger.Assets.HotReload;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.HotReload;

/// <summary>
/// Exercises <see cref="AssetWatcher"/> + <see cref="TextureManager.Reload"/> +
/// <see cref="SceneHotReload"/>: a small scene loaded from <c>scene.json</c> that reloads itself
/// while running whenever the scene file or one of its textures changes on disk.
/// </summary>
/// <remarks>
/// In a Debug build the scene is loaded from — and watched at — the <em>source</em>
/// <c>Assets/HotReload/</c> folder (found by walking up from <see cref="AppContext.BaseDirectory"/>
/// looking for <c>FeatureGallery.csproj</c>), not the build output copy: editing the checked-in
/// file under <c>Samples/FeatureGallery/Assets/HotReload/</c> is what a contributor actually
/// touches, and a build only copies that folder into <c>bin/</c> on the next build. A published
/// (non-Debug) build falls back to the output folder, since the source tree isn't deployed
/// alongside it.
/// </remarks>
public sealed class HotReloadScene : IDemoScene
{
    private const string FontPath = "Assets/Shared/Roboto-Regular.ttf";
    private const string SceneFileName = "scene.json";

    private World? _world;
    private Renderer? _renderer;
    private FontManager? _fontManager;
    private TextRenderer? _textRenderer;
    private UnifiedRenderSystem? _renderSystem;
    private Font? _font;

    private Entity _watchedPathHud;
    private Entity _lastEventHud;

    private string _assetsRoot = "";
    private SceneLoader? _sceneLoader;
    private SceneHotReload? _sceneHotReload;
    private AssetWatcher? _assetWatcher;
    private IReadOnlyList<Entity> _sceneEntities = [];

    public string Name => "Hot Reload";
    public string Description => "Edit scene.json or overwrite a texture and watch it update live";
    public string Controls => "None — edit the files under the path shown in the HUD";

    public void Load(Window window)
    {
        _world = new World();
        _renderer = new Renderer(window);
        _fontManager = new FontManager();
        _textRenderer = new TextRenderer(window);
        _renderSystem = new UnifiedRenderSystem(_renderer, _textRenderer, _world, window);
        _font = _fontManager.Load(FontPath);

        _watchedPathHud = _world.CreateEntity("hud-watched-path");
        _world.AddComponent(
            _watchedPathHud,
            new Transform2D { Position = new Vector2(-0.95f, 0.9f), Scale = new Vector2(0.002f) }
        );
        _world.AddComponent(_watchedPathHud, new Text("", _font, 16, Color.White));

        _lastEventHud = _world.CreateEntity("hud-last-event");
        _world.AddComponent(
            _lastEventHud,
            new Transform2D { Position = new Vector2(-0.95f, 0.8f), Scale = new Vector2(0.002f) }
        );
        _world.AddComponent(_lastEventHud, new Text("", _font, 16, new Color(180, 220, 180)));

        _assetsRoot = ResolveAssetsRoot();

        var registry = new ComponentRegistry().RegisterEngineComponents();
        _sceneLoader = new SceneLoader(registry);

        var scenePath = Path.Combine(_assetsRoot, SceneFileName);
        _sceneEntities = InstantiateScene(_sceneLoader.Load(scenePath));

        _sceneHotReload = new SceneHotReload(_sceneLoader, scenePath);
        _sceneHotReload.Reloaded += OnSceneReloaded;

        _assetWatcher = AssetWatcher.ForDirectory(_assetsRoot);
        _assetWatcher.AssetChanged += OnAssetChanged;

        SetText(_watchedPathHud, $"watching: {_assetsRoot}");
        SetText(_lastEventHud, "waiting for a change...");
    }

    public void Update(float deltaTime)
    {
        _assetWatcher?.Update(deltaTime);
    }

    public void Render(float deltaTime) => _renderSystem?.Render();

    public void Dispose()
    {
        if (_assetWatcher is not null)
        {
            _assetWatcher.AssetChanged -= OnAssetChanged;
            _assetWatcher.Dispose();
        }

        if (_sceneHotReload is not null)
            _sceneHotReload.Reloaded -= OnSceneReloaded;

        _textRenderer?.Dispose();
        _fontManager?.Dispose();
        _renderer?.Dispose();
    }

    private void OnAssetChanged(string path)
    {
        if (string.Equals(path, SceneFileName, StringComparison.OrdinalIgnoreCase))
        {
            _sceneHotReload!.Reload();
            return;
        }

        var absolutePath = Path.Combine(_assetsRoot, path);
        if (_renderer!.Textures.Reload(absolutePath))
            SetText(_lastEventHud, $"reloaded texture {path} {DateTime.Now:HH:mm:ss}");
    }

    private void OnSceneReloaded(Scene scene)
    {
        foreach (var entity in _sceneEntities)
            _world!.DestroyEntity(entity);

        _sceneEntities = InstantiateScene(scene);
        SetText(_lastEventHud, $"reloaded {SceneFileName} {DateTime.Now:HH:mm:ss}");
    }

    private IReadOnlyList<Entity> InstantiateScene(Scene scene)
    {
        var entities = _world!.Instantiate(scene);

        // The scene file's texturePath values are bare filenames (e.g. "square.png") so a single
        // AssetWatcher.AssetChanged dispatch (relative to _assetsRoot) can address both the scene
        // file and any texture the same way. Sprite/TextureManager resolve a relative path against
        // AppContext.BaseDirectory (see AssetPath.Resolve), which isn't _assetsRoot in a Debug
        // build loading from source — so each Sprite's TexturePath is rewritten here to the
        // absolute path under _assetsRoot, the same directory the file was actually loaded from.
        foreach (var entity in entities)
        {
            if (!_world.TryGetComponent<Sprite>(entity, out var sprite))
                continue;

            if (Path.IsPathRooted(sprite.TexturePath))
                continue;

            var absolutePath = Path.Combine(_assetsRoot, sprite.TexturePath);
            _world.AddComponent(
                entity,
                new Sprite(absolutePath, sprite.Tint, sprite.FlipX, sprite.FlipY)
            );
        }

        return entities;
    }

    private void SetText(Entity entity, string content)
    {
        if (_world is null || _font is null)
            return;

        if (_world.TryGetComponent<Text>(entity, out var text))
            text.Content = content;
        else
            text = new Text(content, _font, 16, Color.White);

        _world.AddComponent(entity, text);
    }

    /// <summary>
    /// Debug: the source <c>Assets/HotReload/</c> folder, found by walking up from
    /// <see cref="AppContext.BaseDirectory"/> for the directory containing
    /// <c>FeatureGallery.csproj</c>. Falls back to the build output's own <c>Assets/HotReload/</c>
    /// (also used directly in a non-Debug build) when the project file can't be found, e.g. a
    /// published build.
    /// </summary>
    private static string ResolveAssetsRoot()
    {
#if DEBUG
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
        {
            if (File.Exists(Path.Combine(dir.FullName, "FeatureGallery.csproj")))
                return Path.Combine(dir.FullName, "Assets", "HotReload");
        }
#endif
        return Path.Combine(AppContext.BaseDirectory, "Assets", "HotReload");
    }
}
