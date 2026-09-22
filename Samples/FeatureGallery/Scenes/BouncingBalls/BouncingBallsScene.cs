using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.BouncingBalls;

/// <summary>
/// Physics demo: a container of balls with varying size/mass/restitution bouncing under gravity,
/// with an optional collider-wireframe debug overlay.
/// </summary>
public sealed class BouncingBallsScene : IDemoScene
{
    private const float WallThickness = 0.05f;

    private World? _world;
    private Renderer? _renderer;
    private UnifiedRenderSystem? _renderSystem;
    private PhysicsWorld2D? _physics;
    private PhysicsDebugRenderer? _debugRenderer;
    private bool _showDebug;

    public string Name => "Bouncing Balls";
    public string Description => "Impulse-based 2D physics with varied ball materials";
    public string Controls => "SPACE: toggle collider debug view";

    public void Load(Window window)
    {
        _world = new World();
        _renderer = new Renderer(window);
        _renderSystem = new UnifiedRenderSystem(_renderer, null, _world);

        // Physics world with downward gravity (positive Y is up in NDC).
        // Cell size 0.15 — tuned for this NDC-scale world (≈ 3× the average ball radius of 0.054).
        _physics = new PhysicsWorld2D(_world, new Vector2(0, -2.0f), broadphaseCellSize: 0.15f);
        _debugRenderer = new PhysicsDebugRenderer(window, _world);
        _showDebug = false;

        CreateWall(new Vector2(0, -1.0f), new Vector2(2.0f, WallThickness)); // Floor
        CreateWall(new Vector2(0, 1.0f), new Vector2(2.0f, WallThickness)); // Ceiling
        CreateWall(new Vector2(-1.0f, 0), new Vector2(WallThickness, 2.0f)); // Left
        CreateWall(new Vector2(1.0f, 0), new Vector2(WallThickness, 2.0f)); // Right

        var ballSprite = new Sprite("Assets/BouncingBalls/circle.png");
        var random = new Random(42);

        var ballConfigs = new[]
        {
            new
            {
                Radius = 0.06f,
                Mass = 1.0f,
                Restitution = 0.95f,
                Position = new Vector2(-0.4f, 0.7f),
            },
            new
            {
                Radius = 0.04f,
                Mass = 0.5f,
                Restitution = 0.85f,
                Position = new Vector2(0.0f, 0.5f),
            },
            new
            {
                Radius = 0.08f,
                Mass = 2.0f,
                Restitution = 0.9f,
                Position = new Vector2(0.3f, 0.8f),
            },
            new
            {
                Radius = 0.05f,
                Mass = 0.8f,
                Restitution = 0.8f,
                Position = new Vector2(-0.2f, 0.3f),
            },
            new
            {
                Radius = 0.035f,
                Mass = 0.3f,
                Restitution = 1.0f,
                Position = new Vector2(0.5f, 0.6f),
            },
            new
            {
                Radius = 0.07f,
                Mass = 1.5f,
                Restitution = 0.7f,
                Position = new Vector2(-0.6f, 0.4f),
            },
            new
            {
                Radius = 0.045f,
                Mass = 0.6f,
                Restitution = 0.92f,
                Position = new Vector2(0.2f, 0.9f),
            },
            new
            {
                Radius = 0.055f,
                Mass = 1.2f,
                Restitution = 0.88f,
                Position = new Vector2(-0.5f, 0.6f),
            },
        };

        foreach (var config in ballConfigs)
        {
            var ball = _world.CreateEntity();
            _world.AddComponent(ball, ballSprite);
            _world.AddComponent(
                ball,
                new Transform2D(config.Position, 0.0f, new Vector2(config.Radius * 2))
            );
            _world.AddComponent(ball, RigidBody2D.CreateDynamic(config.Mass, linearDrag: 0.1f));
            _world.AddComponent(
                ball,
                new Velocity2D
                {
                    Linear = new Vector2(
                        (float)(random.NextDouble() * 1.0 - 0.5),
                        (float)(random.NextDouble() * 0.5 - 0.25)
                    ),
                }
            );
            _world.AddComponent(ball, new CircleCollider2D(config.Radius));
            _world.AddComponent(ball, new PhysicsMaterial(config.Restitution, friction: 0.2f));
        }

        Keyboard.AddKeyDown(Keys.Space, ToggleDebug);
    }

    public void Update(float deltaTime) => _physics?.Update(deltaTime);

    public void Render(float deltaTime)
    {
        _renderSystem?.Render();

        if (_showDebug)
            _debugRenderer?.Render();
    }

    private void ToggleDebug() => _showDebug = !_showDebug;

    private void CreateWall(Vector2 position, Vector2 size)
    {
        if (_world is null)
            return;

        var wall = _world.CreateEntity();
        _world.AddComponent(wall, new Sprite("Assets/BouncingBalls/square.png"));
        _world.AddComponent(wall, new Transform2D(position, 0.0f, size));
        _world.AddComponent(wall, RigidBody2D.CreateStatic());
        _world.AddComponent(wall, new BoxCollider2D(size.X, size.Y));
        _world.AddComponent(wall, new PhysicsMaterial(1.0f, friction: 0.2f));
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDown(Keys.Space);

        _debugRenderer?.Dispose();
        _renderer?.Dispose();
    }
}
