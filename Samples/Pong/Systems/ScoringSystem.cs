using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class ScoringSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        var ballEntity = world.GetEntity(EntityTags.Ball);
        var transform = world.GetComponent<Transform2D>(ballEntity);
        var bounds = world.GetComponent<Bounds>(ballEntity);

        // Check for score
        // Ball bounds
        var ballPos = transform.Position;
        var ballScale = transform.Scale;
        var ballHalf = ballScale / 2f;

        // Left player scored
        if (ballPos.X + ballHalf.X > bounds.MaxX)
        {
            HandleScore(EntityTags.LeftPaddle, Player.Right, ballEntity);
        }

        // Right player scored
        if (ballPos.X - ballHalf.X < bounds.MinX)
        {
            HandleScore(EntityTags.RightPaddle, Player.Left, ballEntity);
        }
    }

    private void HandleScore(string playerTag, Player server, Entity ballEntity)
    {
        var playerEntity = world.GetEntity(playerTag);
        var playerScore = world.GetComponent<PlayerScore>(playerEntity);

        world.AddComponent(playerEntity, playerScore with { Score = playerScore.Score + 1 });
        world.AddComponent(ballEntity, new Ball { State = BallState.Scored, Server = server });
    }
}