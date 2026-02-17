using System.Numerics;

using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class ResetBallSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        var ballEntity = world.GetEntity(EntityTags.Ball);

        if (!world.TryGetComponent(ballEntity, out Ball ball) || ball.State != BallState.Scored)
        {
            return;
        }

        // Reset the ball position and velocity
        var transform = world.GetComponent<Transform2D>(ballEntity);
        world.AddComponent(ballEntity, transform with { Position = Vector2.Zero });
        world.AddComponent(ballEntity, new Velocity(Vector2.Zero));

        // Reset the ball state
        world.AddComponent(ballEntity, ball with { State = BallState.Waiting });
    }
}