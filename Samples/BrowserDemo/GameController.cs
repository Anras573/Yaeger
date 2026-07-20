using System.Numerics;
using Microsoft.JSInterop;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics.Components;
using Yaeger.Platform;
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
    private readonly IInputState _input = new BrowserInputState();
    private readonly PaddleControlSystem _paddleSystem;
    private readonly BallMovementSystem _movementSystem;
    private readonly BrowserTimeSource _timeSource = new();

    public GameController(BrowserRenderSurface renderSurface)
    {
        _renderSurface = renderSurface;
        _world = new World();
        _paddleSystem = new PaddleControlSystem(_world, _input);
        _movementSystem = new BallMovementSystem(_world);
        BuildScene();
    }

    private void BuildScene()
    {
        // A paddle the player drives along the bottom edge...
        var paddle = _world.CreateEntity("paddle");
        _world.AddComponent(
            paddle,
            new Transform2D(new Vector2(0f, -0.85f), scale: new Vector2(0.34f, 0.06f))
        );
        _world.AddComponent(paddle, new Sprite("", new Color(80, 200, 255)));

        // ...and an orange ball that bounces around the canvas, off the paddle when it's there
        // to catch it, or back to serve from the top when it isn't.
        var ball = _world.CreateEntity("ball");
        _world.AddComponent(
            ball,
            new Transform2D(new Vector2(0f, 0.6f), scale: new Vector2(0.08f, 0.08f))
        );
        _world.AddComponent(ball, new Sprite("", new Color(255, 140, 0)));
        _world.AddComponent(ball, new Velocity2D(0.45f, -0.6f));
    }

    /// <summary>
    /// Called once per frame by the JavaScript <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser.
    /// </summary>
    [JSInvokable]
    public void Tick(double timestampMs)
    {
        _timeSource.Advance(timestampMs);

        // Snapshot keyboard/mouse/scroll input at the tick boundary so all game systems within
        // this tick see stable values, matching native input behavior.
        BrowserInputState.BeginFrame();

        _paddleSystem.Update(_timeSource.DeltaTime);
        _movementSystem.Update(_timeSource.DeltaTime);
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
/// Drives the paddle entity from keyboard (arrow keys / A-D) or, while the left mouse button
/// or a touch is held, directly under the pointer — giving desktop and touch players an equally
/// direct way to play.
/// </summary>
internal sealed class PaddleControlSystem(World world, IInputState input) : IUpdateSystem
{
    private const float Speed = 1.6f;

    public void Update(float deltaTime)
    {
        if (
            !world.TryGetEntity("paddle", out var paddle)
            || !world.TryGetComponent<Transform2D>(paddle, out var transform)
        )
            return;

        var halfWidth = transform.Scale.X * 0.5f;
        var x = transform.Position.X;

        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            x = input.MousePositionNdc.X;
        }
        else
        {
            if (input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A))
                x -= Speed * deltaTime;
            if (input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D))
                x += Speed * deltaTime;
        }

        var clampedX = Math.Clamp(x, -1f + halfWidth, 1f - halfWidth);
        transform.Position = new Vector2(clampedX, transform.Position.Y);
        world.AddComponent(paddle, transform);
    }
}

/// <summary>
/// Moves the ball via its <see cref="Velocity2D"/>, bouncing it off the side/top NDC edges
/// (±1) and off the paddle when it's there to catch it. A ball that gets past the paddle is
/// re-served from the top rather than ending the game — this is a bounce-practice toy, not a
/// scored game.
/// </summary>
internal sealed class BallMovementSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        // Declared outside the && so it's definitely assigned even when the short-circuit
        // skips TryGetComponent (the compiler can't otherwise prove that from `hasPaddle` alone
        // once it's read back later in a separate `if`).
        var paddleTransform = default(Transform2D);
        var hasPaddle =
            world.TryGetEntity("paddle", out var paddleEntity)
            && world.TryGetComponent(paddleEntity, out paddleTransform);

        foreach (var (entity, velocity, transform) in world.Query<Velocity2D, Transform2D>())
        {
            var pos = transform.Position;
            var vel = velocity.Linear;
            var halfScale = transform.Scale * 0.5f;

            pos += vel * deltaTime;

            // Reflect off the side and top NDC edges (±1) and clamp to prevent tunnelling.
            if (pos.X - halfScale.X < -1f || pos.X + halfScale.X > 1f)
            {
                vel.X = -vel.X;
                pos.X = Math.Clamp(pos.X, -1f + halfScale.X, 1f - halfScale.X);
            }

            if (pos.Y + halfScale.Y > 1f)
            {
                vel.Y = -vel.Y;
                pos.Y = 1f - halfScale.Y;
            }

            // Bounce off the paddle: reflect upward and nudge the X velocity based on where it
            // was hit, Breakout-style, so the player can aim the return.
            if (hasPaddle && vel.Y < 0f)
            {
                var paddleHalf = paddleTransform.Scale * 0.5f;
                var paddlePos = paddleTransform.Position;
                var overlapsX =
                    pos.X + halfScale.X > paddlePos.X - paddleHalf.X
                    && pos.X - halfScale.X < paddlePos.X + paddleHalf.X;
                var paddleTop = paddlePos.Y + paddleHalf.Y;

                if (overlapsX && pos.Y - halfScale.Y <= paddleTop && pos.Y >= paddlePos.Y)
                {
                    vel.Y = -vel.Y;
                    pos.Y = paddleTop + halfScale.Y;
                    vel.X += (pos.X - paddlePos.X) / paddleHalf.X * 0.5f;
                }
            }

            // Missed the paddle: serve a fresh ball back in from the top.
            if (pos.Y + halfScale.Y < -1f)
            {
                pos = new Vector2(0f, 0.6f);
                vel = new Vector2(vel.X >= 0f ? 0.45f : -0.45f, -0.6f);
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
