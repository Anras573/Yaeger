using System.Diagnostics;
using System.Numerics;
using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Pong.Systems;

public class PhysicsSystem(World world) : IUpdateSystem
{
    private long _lastBounce = Stopwatch.GetTimestamp();
    const float BallVelocityIncrement = 1.1f;

    public void Update(float deltaTime)
    {
        if (!world.TryGetEntity(EntityTags.Ball, out var ballEntity))
            return;

        var transform = world.GetComponent<Transform2D>(ballEntity);
        var velocity = world.GetComponent<Velocity>(ballEntity);
        var bounds = world.GetComponent<Bounds>(ballEntity);

        var ballPos = transform.Position;
        var ballScale = transform.Scale;
        var ballHalf = ballScale / 2f;

        if (Stopwatch.GetTimestamp() - _lastBounce < Stopwatch.Frequency / 30)
            return;

        velocity = HandleWallCollisions(ballPos, ballHalf, velocity, bounds);
        velocity = HandlePaddleCollisions(ballPos, ballHalf, velocity);

        world.AddComponent(ballEntity, velocity);
    }

    private Velocity HandlePaddleCollisions(
        Vector2 ballPos,
        Vector2 ballHalf,
        Velocity ballVelocity
    )
    {
        foreach ((_, _, Transform2D paddle) in world.Query<PlayerControlled, Transform2D>())
        {
            var paddlePos = paddle.Position;
            var paddleScale = paddle.Scale;
            var paddleHalf = paddleScale / 2f;
            // AABB collision
            var overlapX = MathF.Abs(ballPos.X - paddlePos.X) < ballHalf.X + paddleHalf.X;
            var overlapY = MathF.Abs(ballPos.Y - paddlePos.Y) < ballHalf.Y + paddleHalf.Y;
            if (!overlapX || !overlapY)
                continue;
            ballVelocity = new Velocity(
                ballVelocity.Value with
                {
                    X = -ballVelocity.Value.X * BallVelocityIncrement,
                }
            );
            _lastBounce = Stopwatch.GetTimestamp();
            break;
        }

        return ballVelocity;
    }

    private Velocity HandleWallCollisions(
        Vector2 ballPos,
        Vector2 ballHalf,
        Velocity ballVelocity,
        Bounds bounds
    )
    {
        if (ballPos.Y + ballHalf.Y > bounds.MaxY)
        {
            ballVelocity = new Velocity(
                ballVelocity.Value with
                {
                    Y = -MathF.Abs(ballVelocity.Value.Y),
                }
            );
            _lastBounce = Stopwatch.GetTimestamp();
        }
        else if (ballPos.Y - ballHalf.Y < bounds.MinY)
        {
            ballVelocity = new Velocity(
                ballVelocity.Value with
                {
                    Y = MathF.Abs(ballVelocity.Value.Y),
                }
            );
            _lastBounce = Stopwatch.GetTimestamp();
        }

        return ballVelocity;
    }
}
