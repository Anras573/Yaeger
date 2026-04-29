using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Pong.Systems;

public class PrintScoreSystem : IUpdateSystem
{
    private readonly World _world;
    private readonly Entity _leftPaddle;
    private readonly Entity _rightPaddle;
    private readonly Entity _leftScore;
    private readonly Entity _rightScore;
    private int _lastLeftScore = -1;
    private int _lastRightScore = -1;

    public PrintScoreSystem(World world)
    {
        _world = world;
        _leftPaddle = world.GetEntity(EntityTags.LeftPaddle);
        _rightPaddle = world.GetEntity(EntityTags.RightPaddle);
        _leftScore = world.GetEntity(EntityTags.LeftScore);
        _rightScore = world.GetEntity(EntityTags.RightScore);
    }

    public void Update(float deltaTime)
    {
        UpdateScore(_leftPaddle, _leftScore, ref _lastLeftScore);
        UpdateScore(_rightPaddle, _rightScore, ref _lastRightScore);
    }

    private void UpdateScore(Entity playerEntity, Entity scoreEntity, ref int lastScore)
    {
        var score = _world.GetComponent<PlayerScore>(playerEntity).Score;
        if (score == lastScore)
            return;

        lastScore = score;
        var scoreText = _world.GetComponent<Text>(scoreEntity);
        _world.AddComponent(scoreEntity, scoreText with { Content = score.ToString() });
    }
}
