using System.Diagnostics;
using System.Numerics;

using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class PhysicsSystem(World world) : IUpdateSystem
{
    private long _lastBounce = Stopwatch.GetTimestamp();
    const float BallVelocityIncrement = 1.1f; // Increment ball speed after each bounce
    public void Update(float deltaTime)
    {
        // Find the ball (assume only one ball, with Velocity and Transform2D)
        (Entity ballEntity, _, Transform2D transform, Velocity velocity, Bounds bounds) = world.Query<Ball, Transform2D, Velocity, Bounds>().First();

        // Ball bounds
        var ballPos = transform.Position;
        var ballScale = transform.Scale;
        var ballHalf = ballScale / 2f;

        // --- Pong-specific collision detection ---

        if (Stopwatch.GetTimestamp() - _lastBounce < Stopwatch.Frequency / 30)
            return;

        velocity = HandleWallCollisions(ballPos, ballHalf, velocity, bounds);
        velocity = HandlePaddleCollisions(ballPos, ballHalf, velocity);

        // Write back ball velocity
        world.AddComponent(ballEntity, velocity);
    }

    private Velocity HandlePaddleCollisions(Vector2 ballPos, Vector2 ballHalf, Velocity ballVelocity)
    {
        // Check collision with paddles
        foreach ((_, Transform2D paddle, _) in world.Query<Transform2D, PlayerControlled>())
        {
            var paddlePos = paddle.Position;
            var paddleScale = paddle.Scale;
            var paddleHalf = paddleScale / 2f;
            // AABB collision
            var overlapX = MathF.Abs(ballPos.X - paddlePos.X) < ballHalf.X + paddleHalf.X;
            var overlapY = MathF.Abs(ballPos.Y - paddlePos.Y) < ballHalf.Y + paddleHalf.Y;
            if (!overlapX || !overlapY) continue;
            // Reverse X velocity
            // Update the ball's velocity
            ballVelocity = new Velocity(ballVelocity.Value with { X = -ballVelocity.Value.X * BallVelocityIncrement });
            _lastBounce = Stopwatch.GetTimestamp();
            break;
        }

        return ballVelocity;
    }

    private Velocity HandleWallCollisions(Vector2 ballPos, Vector2 ballHalf, Velocity ballVelocity, Bounds bounds)
    {
        // Check collision with top/bottom walls (assuming play area is -1 to 1 in Y)
        if (ballPos.Y + ballHalf.Y > bounds.MaxY)
        {
            ballVelocity = new Velocity(ballVelocity.Value with { Y = -MathF.Abs(ballVelocity.Value.Y) });
            _lastBounce = Stopwatch.GetTimestamp();
        }
        else if (ballPos.Y - ballHalf.Y < bounds.MinY)
        {
            ballVelocity = new Velocity(ballVelocity.Value with { Y = MathF.Abs(ballVelocity.Value.Y) });
            _lastBounce = Stopwatch.GetTimestamp();
        }

        return ballVelocity;
    }
}