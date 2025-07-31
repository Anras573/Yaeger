using Pong.Components;

using Yaeger.ECS;

namespace Pong.Systems;

public class PrintScoreSystem(World world, Entity leftPlayerEntity, Entity rightPlayerEntity) : IUpdateSystem
{
    private int _leftScore = 0;
    private int _rightScore = 0;
    
    public void Update(float deltaTime)
    {
        world.TryGetComponent(leftPlayerEntity, out PlayerScore leftPlayerScore);
        world.TryGetComponent(rightPlayerEntity, out PlayerScore rightPlayerScore);
        
        if (leftPlayerScore.Score != _leftScore)
        {
            _leftScore = leftPlayerScore.Score;
            Console.WriteLine($"Left Player Score: {_leftScore}");
        }

        if (rightPlayerScore.Score != _rightScore)
        {
            _rightScore = rightPlayerScore.Score;
            Console.WriteLine($"Right Player Score: {_rightScore}");
        }
    }
}