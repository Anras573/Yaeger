using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

using var window = Window.Create();
var world = new World();
var renderer = new Renderer(window);
var renderSystem = new RenderSystem(renderer, world);

// Physics world with downward gravity (positive Y is up in NDC)
var physics = new PhysicsWorld2D(world, new Vector2(0, -2.0f));

// --- Walls (static boxes forming a container) ---
var wallThickness = 0.05f;
var wallSprite = new Sprite("Assets/square.png");

// Floor
var floor = world.CreateEntity();
world.AddComponent(floor, wallSprite);
world.AddComponent(
    floor,
    new Transform2D(new Vector2(0, -1.0f), 0.0f, new Vector2(2.0f, wallThickness))
);
world.AddComponent(floor, RigidBody2D.CreateStatic());
world.AddComponent(floor, new BoxCollider2D(2.0f, wallThickness));
world.AddComponent(floor, PhysicsMaterial.Default);

// Ceiling
var ceiling = world.CreateEntity();
world.AddComponent(ceiling, wallSprite);
world.AddComponent(
    ceiling,
    new Transform2D(new Vector2(0, 1.0f), 0.0f, new Vector2(2.0f, wallThickness))
);
world.AddComponent(ceiling, RigidBody2D.CreateStatic());
world.AddComponent(ceiling, new BoxCollider2D(2.0f, wallThickness));
world.AddComponent(ceiling, PhysicsMaterial.Default);

// Left wall
var leftWall = world.CreateEntity();
world.AddComponent(leftWall, wallSprite);
world.AddComponent(
    leftWall,
    new Transform2D(new Vector2(-1.0f, 0), 0.0f, new Vector2(wallThickness, 2.0f))
);
world.AddComponent(leftWall, RigidBody2D.CreateStatic());
world.AddComponent(leftWall, new BoxCollider2D(wallThickness, 2.0f));
world.AddComponent(leftWall, PhysicsMaterial.Default);

// Right wall
var rightWall = world.CreateEntity();
world.AddComponent(rightWall, wallSprite);
world.AddComponent(
    rightWall,
    new Transform2D(new Vector2(1.0f, 0), 0.0f, new Vector2(wallThickness, 2.0f))
);
world.AddComponent(rightWall, RigidBody2D.CreateStatic());
world.AddComponent(rightWall, new BoxCollider2D(wallThickness, 2.0f));
world.AddComponent(rightWall, PhysicsMaterial.Default);

// --- Balls ---
var ballSprite = new Sprite("Assets/circle.png");
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
    var ball = world.CreateEntity();
    world.AddComponent(ball, ballSprite);
    world.AddComponent(
        ball,
        new Transform2D(config.Position, 0.0f, new Vector2(config.Radius * 2))
    );
    world.AddComponent(ball, RigidBody2D.CreateDynamic(config.Mass, linearDrag: 0.1f));
    world.AddComponent(
        ball,
        new Velocity2D
        {
            Linear = new Vector2(
                (float)(random.NextDouble() * 1.0 - 0.5),
                (float)(random.NextDouble() * 0.5 - 0.25)
            ),
        }
    );
    world.AddComponent(ball, new CircleCollider2D(config.Radius));
    world.AddComponent(ball, new PhysicsMaterial(config.Restitution, friction: 0.2f));
}

// --- Event wiring ---
window.OnUpdate += delta =>
{
    physics.Update((float)delta);
};

window.OnRender += _ =>
{
    renderSystem.Render();
};

Keyboard.AddKeyDown(Keys.Escape, () => window.Close());

window.Run();
