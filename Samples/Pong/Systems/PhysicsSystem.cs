using System.Diagnostics;
using System.Numerics;
using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Pong.Systems;

public class PhysicsSystem : IUpdateSystem
{
    private readonly World _world;
    private readonly Entity _ballEntity;
    private long _lastBounce;
    const float BallVelocityIncrement = 1.1f;

    public PhysicsSystem(World world)
    {
        _world = world;
        _ballEntity = world.GetEntity(EntityTags.Ball);
        _lastBounce = Stopwatch.GetTimestamp();
    }

    public void Update(float deltaTime)
    {
        var transform = _world.GetComponent<Transform2D>(_ballEntity);
        var velocity = _world.GetComponent<Velocity>(_ballEntity);
        var bounds = _world.GetComponent<Bounds>(_ballEntity);

        var ballPos = transform.Position;
        var ballScale = transform.Scale;
        var ballHalf = ballScale / 2f;

        if (Stopwatch.GetTimestamp() - _lastBounce < Stopwatch.Frequency / 30)
            return;

        velocity = HandleWallCollisions(ballPos, ballHalf, velocity, bounds);
        velocity = HandlePaddleCollisions(ballPos, ballHalf, velocity);

        _world.AddComponent(_ballEntity, velocity);
    }

    private Velocity HandlePaddleCollisions(
        Vector2 ballPos,
        Vector2 ballHalf,
        Velocity ballVelocity
    )
    {
        foreach ((_, Transform2D paddle, _) in _world.Query<Transform2D, PlayerControlled>())
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
