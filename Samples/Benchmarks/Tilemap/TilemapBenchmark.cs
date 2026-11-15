using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace Benchmarks.TileRendering;

/// <summary>
/// Tilemap submission stress test: one large map viewed by a zoomed-out camera, so every frame
/// submits (almost) every tile. Measures the CPU cost of expanding a static map into quads —
/// the path <c>UnifiedRenderSystem</c> caches tile UVs for. FPS is a proxy for CPU-side
/// submission, not GPU fill rate.
/// </summary>
public sealed class TilemapBenchmark : IBenchmark
{
    private const string TexturePath = "Assets/Sprites/square.png";

    public string Arg => "tilemap";
    public string Description =>
        "Large static tilemap viewed zoomed out (count = map side, in tiles)";
    public int DefaultCount => 256;

    public void Run(int count)
    {
        using var window = Window.Create();

        var world = new World();
        var renderer = new Renderer(window);
        var renderSystem = new UnifiedRenderSystem(renderer, null, world, window);

        // Cycle through the tileset so neighbouring tiles differ, like a real map.
        var tileset = new Tileset(TexturePath, columns: 1, rows: 1);
        var tiles = new int[count * count];
        var random = new Random(42);
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = random.Next(8) == 0 ? Tilemap.EmptyTile : 0;

        // A tile is 2 / count world units tall, so the whole map fits a zoom-1 camera view.
        var tileSize = new Vector2(2f / count);
        var map = world.CreateEntity();
        world.AddComponent(map, new Transform2D(new Vector2(-1f, -1f)));
        world.AddComponent(map, new Tilemap(tileset, count, count, tiles, tileSize));

        var camera = world.CreateEntity();
        world.AddComponent(camera, new Camera2D(Vector2.Zero, Zoom: 1f));

        var visibleTiles = count * count;
        Console.WriteLine(
            $"Tilemap benchmark - {count}x{count} tiles ({visibleTiles} cells), zoomed out"
        );
        Console.WriteLine("Press ESC to exit");

        var stats = new FrameStats("Tilemap");
        Keyboard.AddKeyDown(Keys.Escape, window.Close);

        window.OnRender += delta =>
        {
            stats.Tick(delta, () => $"{count}x{count} tiles");
            renderSystem.Render();
        };

        window.Run();
    }
}
