using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.Mouse;

/// <summary>
/// Mouse input demo: paint a trail of sprites while the left button is held, right-click to
/// clear. Demonstrates polling (<c>IsButtonPressed</c>), events (<c>AddButtonDown</c>,
/// <c>AddScroll</c>), and NDC position derivation from pixel input.
/// </summary>
public sealed class MouseScene : IDemoScene
{
    private const string SpriteTexture = "Assets/Mouse/square.png";
    private const string FontPath = "Assets/Shared/Roboto-Regular.ttf";
    private const float MinScale = 0.01f;
    private const float MaxScale = 0.15f;
    private const float ScrollSensitivity = 0.005f;

    private readonly List<Entity> _paintedEntities = [];

    private World? _world;
    private Renderer? _renderer;
    private FontManager? _fontManager;
    private TextRenderer? _textRenderer;
    private UnifiedRenderSystem? _renderSystem;
    private Font? _font;
    private Entity _hudEntity;
    private float _spriteScale = 0.03f;
    private Action<float>? _onScroll;

    public string Name => "Mouse";
    public string Description => "Paint trail, scroll to resize, live NDC readout";
    public string Controls => "LMB (hold): paint   RMB: clear   Scroll: resize";

    public void Load(Window window)
    {
        _world = new World();
        _renderer = new Renderer(window);
        _fontManager = new FontManager();
        _textRenderer = new TextRenderer(window);
        _renderSystem = new UnifiedRenderSystem(_renderer, _textRenderer, _world, window);
        _font = _fontManager.Load(FontPath);

        _hudEntity = _world.CreateEntity("hud");
        _world.AddComponent(_hudEntity, new Text("", _font, 18, Color.White));
        _world.AddComponent(
            _hudEntity,
            new Transform2D { Position = new Vector2(-0.95f, 0.9f), Scale = new Vector2(0.003f) }
        );

        Yaeger.Input.Mouse.AddButtonDown(MouseButton.Right, ClearPainted);
        _onScroll = delta =>
            _spriteScale = Math.Clamp(_spriteScale + delta * ScrollSensitivity, MinScale, MaxScale);
        Yaeger.Input.Mouse.AddScroll(_onScroll);
    }

    public void Update(float deltaTime)
    {
        if (_world is null || _font is null)
            return;

        if (Yaeger.Input.Mouse.IsButtonPressed(MouseButton.Left))
        {
            var entity = _world.CreateEntity();
            _world.AddComponent(entity, new Sprite(SpriteTexture));
            _world.AddComponent(
                entity,
                new Transform2D(Yaeger.Input.Mouse.PositionNdc, 0f, new Vector2(_spriteScale))
            );
            _paintedEntities.Add(entity);
        }

        var hud = new Text(
            $"px ({Yaeger.Input.Mouse.Position.X:F0}, {Yaeger.Input.Mouse.Position.Y:F0})   ndc ({Yaeger.Input.Mouse.PositionNdc.X:F2}, {Yaeger.Input.Mouse.PositionNdc.Y:F2})   size {_spriteScale:F3}   painted {_paintedEntities.Count}",
            _font,
            18,
            Color.White
        );
        _world.AddComponent(_hudEntity, hud);
    }

    public void Render(float deltaTime) => _renderSystem?.Render();

    private void ClearPainted()
    {
        if (_world is null)
            return;

        foreach (var entity in _paintedEntities)
            _world.DestroyEntity(entity);
        _paintedEntities.Clear();
    }

    public void Dispose()
    {
        Yaeger.Input.Mouse.RemoveButtonDown(MouseButton.Right);
        if (_onScroll is not null)
            Yaeger.Input.Mouse.RemoveScroll(_onScroll);

        _textRenderer?.Dispose();
        _fontManager?.Dispose();
        _renderer?.Dispose();
    }
}
