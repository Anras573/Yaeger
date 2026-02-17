using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class PrintScoreSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        UpdateScore(EntityTags.LeftPaddle, EntityTags.LeftScore);
        UpdateScore(EntityTags.RightPaddle, EntityTags.RightScore);
    }

    private void UpdateScore(string playerTag, string scoreTag)
    {
        var player = world.GetEntity(playerTag);
        var playerScore = world.GetComponent<PlayerScore>(player);

        var scoreEntity = world.GetEntity(scoreTag);
        var scoreText = world.GetComponent<Text>(scoreEntity);

        world.AddComponent(scoreEntity, scoreText with { Content = playerScore.Score.ToString() });
    }
}