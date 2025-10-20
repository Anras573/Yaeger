using System.Numerics;

using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Windowing;

namespace BatchRenderingExample;

/// <summary>
/// This example demonstrates batch rendering in Yaeger.
/// 
/// Batch rendering groups draw calls by texture to minimize GPU state changes,
/// which is essential for efficient font rendering and rendering many sprites.
/// 
/// Press SPACE to toggle between batch rendering and individual rendering.
/// Press ESC to exit.
/// </summary>
class Program
{
    private const int SpriteCount = 500;
    private const string TexturePath = "Assets/square.png";

    private static readonly List<SpriteData> Sprites = new();
    private static bool useBatchRendering = true;

    static void Main(string[] args)
    {
        using var window = Window.Create();
        var renderer = new Renderer(window);
        var batchRenderer = new BatchRenderer(window);

        // Initialize sprites with random positions, scales, and velocities
        var random = new Random(42);
        for (int i = 0; i < SpriteCount; i++)
        {
            Sprites.Add(new SpriteData
            {
                Position = new Vector2(
                    (float)random.NextDouble() * 2f - 1f,
                    (float)random.NextDouble() * 2f - 1f
                ),
                Scale = (float)random.NextDouble() * 0.05f + 0.02f,
                Velocity = new Vector2(
                    (float)random.NextDouble() * 0.5f - 0.25f,
                    (float)random.NextDouble() * 0.5f - 0.25f
                ),
                Rotation = (float)random.NextDouble() * MathF.PI * 2f,
                RotationSpeed = (float)random.NextDouble() * 2f - 1f
            });
        }

        Console.WriteLine($"Batch Rendering Example - Rendering {SpriteCount} sprites");
        Console.WriteLine("Press SPACE to toggle between batch and individual rendering");
        Console.WriteLine("Press ESC to exit");

        int fps = 0;
        double lastFpsTime = 0;

        window.OnUpdate += Update;
        window.OnRender += Render;
        window.Run();

        return;

        void Update(double deltaTime)
        {
            // Toggle rendering mode
            if (Keyboard.IsKeyPressed(Keys.Space))
            {
                useBatchRendering = !useBatchRendering;
                Console.WriteLine($"Switched to {(useBatchRendering ? "BATCH" : "INDIVIDUAL")} rendering");
            }

            if (Keyboard.IsKeyPressed(Keys.Escape))
            {
                window.Close();
            }

            // Update sprite positions and rotations
            foreach (var sprite in Sprites)
            {
                sprite.Position += sprite.Velocity * (float)deltaTime;
                sprite.Rotation += sprite.RotationSpeed * (float)deltaTime;

                // Bounce off edges
                if (sprite.Position.X < -1f || sprite.Position.X > 1f)
                {
                    var vel = sprite.Velocity;
                    vel.X *= -1f;
                    sprite.Velocity = vel;
                    var pos = sprite.Position;
                    pos.X = Math.Clamp(pos.X, -1f, 1f);
                    sprite.Position = pos;
                }
                if (sprite.Position.Y < -1f || sprite.Position.Y > 1f)
                {
                    var vel = sprite.Velocity;
                    vel.Y *= -1f;
                    sprite.Velocity = vel;
                    var pos = sprite.Position;
                    pos.Y = Math.Clamp(pos.Y, -1f, 1f);
                    sprite.Position = pos;
                }
            }
        }

        void Render(double deltaTime)
        {
            fps++;
            lastFpsTime += deltaTime;

            if (lastFpsTime >= 1.0)
            {
                Console.WriteLine($"FPS: {fps} ({(useBatchRendering ? "BATCH" : "INDIVIDUAL")} rendering, {SpriteCount} sprites)");
                fps = 0;
                lastFpsTime = 0;
            }

            if (useBatchRendering)
            {
                RenderWithBatching(batchRenderer);
            }
            else
            {
                RenderIndividually(renderer);
            }
        }
    }

    private static void RenderWithBatching(BatchRenderer batchRenderer)
    {
        batchRenderer.BeginFrame();

        foreach (var sprite in Sprites)
        {
            var transform = CreateTransform(sprite);
            batchRenderer.SubmitQuad(transform, TexturePath);
        }

        batchRenderer.EndFrame();
    }

    private static void RenderIndividually(Renderer renderer)
    {
        renderer.BeginFrame();

        foreach (var sprite in Sprites)
        {
            var transform = CreateTransform(sprite);
            renderer.DrawQuad(transform, TexturePath);
        }

        renderer.EndFrame();
    }

    private static Matrix4x4 CreateTransform(SpriteData sprite)
    {
        return Matrix4x4.CreateScale(sprite.Scale) *
               Matrix4x4.CreateRotationZ(sprite.Rotation) *
               Matrix4x4.CreateTranslation(new Vector3(sprite.Position, 0f));
    }
}

class SpriteData
{
    public Vector2 Position { get; set; }
    public float Scale { get; set; }
    public Vector2 Velocity { get; set; }
    public float Rotation { get; set; }
    public float RotationSpeed { get; set; }
}