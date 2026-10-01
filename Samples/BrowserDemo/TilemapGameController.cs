using System.Numerics;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Platform;
using Yaeger.Systems;

namespace BrowserDemo;

/// <summary>
/// Second browser scene: a camera-followed tilemap rendered through the shared
/// <see cref="UnifiedRenderSystem"/> and <see cref="CameraFollowSystem"/> — the same systems the
/// native samples use. The <see cref="BrowserRenderSurface"/> doubles as the
/// <see cref="IViewport"/>, so the aspect ratio tracks canvas resizes. Select it with
/// <c>?scene=tilemap</c>. Move with the arrow keys / WASD.
/// </summary>
public sealed class TilemapGameController
{
    private const int MapWidth = 40;
    private const int MapHeight = 16;
    private static readonly Vector2 TileSize = new(0.25f, 0.25f);

    private readonly World _world = new();
    private readonly BrowserRenderSurface _renderSurface;
    private readonly IInputState _input = new BrowserInputState();
    private readonly BrowserTimeSource _timeSource = new();
    private readonly CameraFollowSystem _cameraFollow;
    private readonly UnifiedRenderSystem _renderSystem;

    public TilemapGameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _cameraFollow = new CameraFollowSystem(_world, renderSurface);
        _renderSystem = new UnifiedRenderSystem(renderSurface, null, _world, renderSurface);
        BrowserInputState.SetPreventDefaultKeys([Keys.Left, Keys.Right, Keys.Up, Keys.Down]);
        BuildScene();
    }

    private void BuildScene()
    {
        // The solid ("") texture is a 1x1 white pixel, so a one-tile tileset gives flat colour
        // tiles that the per-map tint colours — no image assets needed.
        var tileset = new Tileset(IRenderSurface.SolidTexturePath, columns: 1, rows: 1);

        var ground = new Tilemap(tileset, MapWidth, MapHeight, TileSize, new Color(60, 90, 60));
        for (var column = 0; column < MapWidth; column++)
        {
            ground.Tiles[(MapHeight - 1) * MapWidth + column] = 0;
            if (column % 7 == 3)
                ground.Tiles[(MapHeight - 2) * MapWidth + column] = 0;
        }

        // A checkerboard backdrop on a lower layer proves RenderLayer ordering: it sits
        // *behind* the ground even though it is created later (higher entity id).
        var ground2D = new Transform2D(new Vector2(0f, -1f));
        var groundEntity = _world.CreateEntity("ground");
        _world.AddComponent(groundEntity, ground2D);
        _world.AddComponent(groundEntity, ground);
        _world.AddComponent(groundEntity, new RenderLayer(1));

        var backdrop = new Tilemap(tileset, MapWidth, MapHeight, TileSize, new Color(30, 40, 70));
        for (var i = 0; i < backdrop.Tiles.Length; i++)
            backdrop.Tiles[i] = (i % MapWidth + i / MapWidth) % 2 == 0 ? 0 : Tilemap.EmptyTile;
        var backdropEntity = _world.CreateEntity("backdrop");
        _world.AddComponent(backdropEntity, ground2D);
        _world.AddComponent(backdropEntity, backdrop);
        _world.AddComponent(backdropEntity, new RenderLayer(0));

        var player = _world.CreateEntity("player");
        _world.AddComponent(
            player,
            new Transform2D(new Vector2(0.5f, -0.5f), scale: new Vector2(0.2f, 0.2f))
        );
        _world.AddComponent(
            player,
            new Sprite(IRenderSurface.SolidTexturePath, new Color(255, 140, 0))
        );
        _world.AddComponent(player, new RenderLayer(2));

        var camera = _world.CreateEntity("camera");
        _world.AddComponent(camera, new Camera2D(Vector2.Zero));
        _world.AddComponent(camera, new CameraFollow(player, smoothing: 6f));
        _world.AddComponent(camera, CameraBounds.FromTilemap(ground, ground2D));
    }

    public void Tick(double timestampMs)
    {
        _timeSource.Advance(timestampMs);
        BrowserInputState.BeginFrame();
        var dt = _timeSource.DeltaTime;

        MovePlayer(dt);
        _cameraFollow.Update(dt);

        _renderSurface.BeginFrame();
        _renderSystem.Render();
        _renderSurface.EndFrame();
    }

    private void MovePlayer(float deltaTime)
    {
        if (
            !_world.TryGetEntity("player", out var player)
            || !_world.TryGetComponent<Transform2D>(player, out var transform)
        )
            return;

        var direction = Vector2.Zero;
        if (_input.IsKeyPressed(Keys.Left) || _input.IsKeyPressed(Keys.A))
            direction.X -= 1f;
        if (_input.IsKeyPressed(Keys.Right) || _input.IsKeyPressed(Keys.D))
            direction.X += 1f;
        if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W))
            direction.Y += 1f;
        if (_input.IsKeyPressed(Keys.Down) || _input.IsKeyPressed(Keys.S))
            direction.Y -= 1f;

        if (direction != Vector2.Zero)
            direction = Vector2.Normalize(direction);

        var maxPosition = new Vector2(MapWidth, MapHeight) * TileSize + new Vector2(0f, -1f);
        transform.Position = Vector2.Clamp(
            transform.Position + direction * 1.2f * deltaTime,
            new Vector2(0f, -1f),
            maxPosition
        );
        _world.AddComponent(player, transform);
    }
}
