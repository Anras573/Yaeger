using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace BrowserDemo;

/// <summary>
/// Owns the ECS world and drives the game loop.  Each tick is invoked by JavaScript's
/// <c>requestAnimationFrame</c> via <see cref="Tick"/>.
/// </summary>
public sealed class GameController
{
    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly BallMovementSystem _movementSystem;
    private double _lastTimestampMs;

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _world = new World();
        _movementSystem = new BallMovementSystem(_world);
        BuildScene();
    }

    private void BuildScene()
    {
        // A single orange square that bounces around the canvas.
        var ball = _world.CreateEntity("ball");
        _world.AddComponent(ball, new Transform2D(Vector2.Zero, scale: new Vector2(0.15f, 0.15f)));
        _world.AddComponent(ball, new Sprite("", new Color(255, 140, 0)));
        _world.AddComponent(ball, new Velocity2D(0.4f, 0.6f));
    }

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs)
    {
        var deltaTime =
            _lastTimestampMs > 0 ? (float)((timestampMs - _lastTimestampMs) / 1000.0) : 0.0f;
        _lastTimestampMs = timestampMs;

        _movementSystem.Update(deltaTime);
        Render();
    }

    private void Render()
    {
        _renderSurface.BeginFrame();

        foreach (var (_, sprite, transform) in _world.Query<Sprite, Transform2D>())
            _renderSurface.SubmitQuad(
                transform.TransformMatrix,
                sprite.TexturePath,
                sprite.Tint.ToVector4()
            );

        _renderSurface.EndFrame();
    }
}

/// <summary>
/// Moves entities with <see cref="Velocity2D"/> and <see cref="Transform2D"/>, reflecting off
/// the NDC edges (±1) to keep the entity on screen.
/// </summary>
internal sealed class BallMovementSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        foreach (var (entity, velocity, transform) in world.Query<Velocity2D, Transform2D>())
        {
            var pos = transform.Position;
            var vel = velocity.Linear;
            var halfScale = transform.Scale * 0.5f;

            pos += vel * deltaTime;

            // Reflect off NDC edges (±1) and clamp to prevent tunnelling.
            if (pos.X - halfScale.X < -1f || pos.X + halfScale.X > 1f)
            {
                vel.X = -vel.X;
                pos.X = Math.Clamp(pos.X, -1f + halfScale.X, 1f - halfScale.X);
            }

            if (pos.Y - halfScale.Y < -1f || pos.Y + halfScale.Y > 1f)
            {
                vel.Y = -vel.Y;
                pos.Y = Math.Clamp(pos.Y, -1f + halfScale.Y, 1f - halfScale.Y);
            }

            var newTransform = transform;
            newTransform.Position = pos;
            world.AddComponent(entity, newTransform);

            var newVelocity = velocity;
            newVelocity.Linear = vel;
            world.AddComponent(entity, newVelocity);
        }
    }
}
