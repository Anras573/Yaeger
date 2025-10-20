using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class ScoringSystem(World world, Entity ballEntity, Entity leftPlayerEntity, Entity rightPlayerEntity) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        world.TryGetComponent(ballEntity, out Transform2D transform);
        world.TryGetComponent(ballEntity, out Bounds bounds);

        // Check for score
        // Ball bounds
        var ballPos = transform.Position;
        var ballScale = transform.Scale;
        var ballHalf = ballScale / 2f;

        if (ballPos.X + ballHalf.X > bounds.MaxX)
        {
            // Left player scored
            world.AddComponent(ballEntity, new Ball { State = BallState.Scored, Server = Player.Right });

            world.TryGetComponent(leftPlayerEntity, out PlayerScore leftScore);
            world.AddComponent(leftPlayerEntity, leftScore with { Score = leftScore.Score + 1 });
            return;
        }

        if (ballPos.X - ballHalf.X < bounds.MinX)
        {
            // Right player scored
            world.AddComponent(ballEntity, new Ball { State = BallState.Scored, Server = Player.Left });

            world.TryGetComponent(rightPlayerEntity, out PlayerScore rightScore);
            world.AddComponent(rightPlayerEntity, rightScore with { Score = rightScore.Score + 1 });
            return;
        }
    }
}