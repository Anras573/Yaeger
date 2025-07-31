using System.Numerics;

using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class ResetBallSystem(World world, Entity ballEntity) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        if (world.TryGetComponent(ballEntity, out Ball ball) && ball.State == BallState.Scored)
        {
            // Reset the ball position and velocity
            world.TryGetComponent(ballEntity, out Transform2D transform);
            world.AddComponent(ballEntity, transform with { Position = Vector2.Zero });
            world.AddComponent(ballEntity, new Velocity(Vector2.Zero));
            
            // Reset the ball state
            world.AddComponent(ballEntity, ball with { State = BallState.Waiting });
        }
    }
}