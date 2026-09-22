using System.Numerics;
using Silk.NET.OpenGL;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.UI;
using Yaeger.Windowing;

namespace FeatureGallery;

/// <summary>
/// Owns the <see cref="Window"/>, the menu UI, and the currently active <see cref="IDemoScene"/>.
/// Forwards <c>OnUpdate</c>/<c>OnRender</c>/<c>OnResize</c> to whichever is active, disposes the
/// old scene before loading the next, and resets GL state between them so nothing a scene left
/// enabled (depth test, culling, blending, a bound FBO) bleeds into the next one.
/// </summary>
public sealed class SceneHost
{
    private readonly Window _window;
    private readonly (string Name, string Description, Func<IDemoScene> Factory)[] _scenes;

    private readonly World _menuWorld = new();
    private readonly FontManager _fontManager = new();
    private readonly TextRenderer _textRenderer;
    private readonly UiRenderer _uiRenderer;
    private readonly UiSystem _uiSystem;
    private readonly UiRenderSystem _uiRenderSystem;
    private readonly Font _font;

    private readonly List<Entity> _menuEntities = [];
    private readonly List<Entity> _hudEntities = [];

    private IDemoScene? _activeScene;

    public SceneHost(Window window, Func<IDemoScene>[] sceneFactories, string? initialScene)
    {
        _window = window;
        _scenes = sceneFactories
            .Select(factory =>
            {
                // Constructing a scene just to read its metadata is cheap: Load() (which does
                // the actual asset/GL work) hasn't been called, so there's nothing to dispose.
                var probe = factory();
                return (probe.Name, probe.Description, factory);
            })
            .ToArray();

        _font = _fontManager.Load("Assets/Shared/Roboto-Regular.ttf");
        _textRenderer = new TextRenderer(window, _fontManager);
        _uiRenderer = new UiRenderer(window);
        _uiSystem = new UiSystem(_menuWorld);
        _uiRenderSystem = new UiRenderSystem(_menuWorld, _uiRenderer, _textRenderer, _font, window);

        window.OnLoad += OnLoad;
        window.OnUpdate += OnUpdate;
        window.OnRender += OnRender;
        window.OnResize += OnResize;
        window.OnClosing += OnClosing;

        Keyboard.AddKeyDown(Keys.Escape, OnEscape);

        var startScene = initialScene is not null
            ? _scenes.FirstOrDefault(s =>
                string.Equals(s.Name, initialScene, StringComparison.OrdinalIgnoreCase)
            )
            : default;

        if (startScene.Factory is not null)
            EnterScene(startScene.Factory);
    }

    private void OnLoad()
    {
        if (_activeScene is null)
            BuildMenu();
    }

    private void OnEscape()
    {
        if (_activeScene is not null)
            ReturnToMenu();
        else
            _window.Close();
    }

    private void OnUpdate(double deltaTime)
    {
        if (_activeScene is { } scene)
        {
            scene.Update((float)deltaTime);
            return;
        }

        _uiSystem.Update((float)deltaTime);
        HandleMenuClicks();
    }

    private void OnRender(double deltaTime)
    {
        if (_activeScene is { } scene)
        {
            scene.Render((float)deltaTime);
            _uiRenderSystem.Render();
            return;
        }

        _uiRenderer.Clear(new Color(20, 20, 28));
        _uiRenderSystem.Render();
    }

    private void OnResize(Vector2 size)
    {
        if (_activeScene is { } scene)
        {
            scene.Resize(size);
            RebuildHud(scene);
        }
        else
        {
            ClearMenu();
            BuildMenu();
        }
    }

    private void OnClosing()
    {
        _activeScene?.Dispose();
        _uiRenderer.Dispose();
        _textRenderer.Dispose();
        _fontManager.Dispose();
    }

    private void HandleMenuClicks()
    {
        foreach (var (name, _, factory) in _scenes)
        {
            var tag = MenuButtonTag(name);
            if (
                _menuWorld.TryGetEntity(tag, out var button)
                && _menuWorld.TryGetComponent<UiButtonState>(button, out var state)
                && state.WasClicked
            )
            {
                EnterScene(factory);
                return;
            }
        }
    }

    private void EnterScene(Func<IDemoScene> factory)
    {
        ClearMenu();
        ResetGlState();

        var scene = factory();
        scene.Load(_window);
        _activeScene = scene;

        BuildHud(scene);
    }

    private void ReturnToMenu()
    {
        _activeScene?.Dispose();
        _activeScene = null;

        ClearHud();
        ResetGlState();
        BuildMenu();
    }

    private void ResetGlState()
    {
        var gl = _window.Gl;
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.Blend);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        gl.Viewport(0, 0, (uint)_window.Size.X, (uint)_window.Size.Y);
    }

    private static string MenuButtonTag(string sceneName) => $"gallery-menu-btn-{sceneName}";

    private void BuildMenu()
    {
        var builder = new UiBuilder(_menuWorld, _window.Size);

        const float buttonWidth = 320f;
        const float buttonHeight = 56f;
        const float gap = 14f;

        var panelWidth = 420f;
        var panelHeight = 100f + _scenes.Length * (buttonHeight + gap);
        var panelX = builder.CenterX(panelWidth);
        var panelY = builder.CenterY(panelHeight);

        _menuEntities.Add(
            builder.CreatePanel(panelX, panelY, panelWidth, panelHeight, new Color(24, 24, 32, 235))
        );
        _menuEntities.Add(
            builder.CreateLabel(panelX + 24f, panelY + 20f, "Feature Gallery", 28, Color.White)
        );

        var buttonX = builder.CenterX(buttonWidth);
        var y = panelY + 70f;

        foreach (var (name, description, _) in _scenes)
        {
            _menuEntities.Add(
                builder.CreateButton(
                    buttonX,
                    y,
                    buttonWidth,
                    buttonHeight,
                    new Color(55, 65, 90),
                    new Color(80, 95, 130),
                    new Color(35, 42, 60),
                    tag: MenuButtonTag(name)
                )
            );
            _menuEntities.Add(builder.CreateLabel(buttonX + 16f, y + 8f, name, 22, Color.White));
            _menuEntities.Add(
                builder.CreateLabel(
                    buttonX + 16f,
                    y + 32f,
                    description,
                    13,
                    new Color(200, 200, 210)
                )
            );

            y += buttonHeight + gap;
        }

        _menuEntities.Add(
            builder.CreateLabel(
                panelX + 24f,
                panelY + panelHeight - 30f,
                "ESC: exit",
                14,
                new Color(160, 160, 170)
            )
        );
    }

    private void ClearMenu()
    {
        foreach (var entity in _menuEntities)
            _menuWorld.DestroyEntity(entity);
        _menuEntities.Clear();
    }

    private void BuildHud(IDemoScene scene)
    {
        var builder = new UiBuilder(_menuWorld, _window.Size);

        var text = $"{scene.Name}: {scene.Controls}   |   ESC: menu";
        _hudEntities.Add(builder.CreatePanel(0, 0, _window.Size.X, 30f, new Color(0, 0, 0, 160)));
        _hudEntities.Add(builder.CreateLabel(10f, 6f, text, 16, Color.White));
    }

    private void RebuildHud(IDemoScene scene)
    {
        ClearHud();
        BuildHud(scene);
    }

    private void ClearHud()
    {
        foreach (var entity in _hudEntities)
            _menuWorld.DestroyEntity(entity);
        _hudEntities.Clear();
    }
}
