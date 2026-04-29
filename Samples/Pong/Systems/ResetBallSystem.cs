using System.Numerics;
using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Pong.Systems;

public class ResetBallSystem : IUpdateSystem
{
    private readonly World _world;
    private readonly Entity _ballEntity;

    public ResetBallSystem(World world)
    {
        _world = world;
        _ballEntity = world.GetEntity(EntityTags.Ball);
    }

    public void Update(float deltaTime)
    {
        if (!_world.TryGetComponent(_ballEntity, out Ball ball) || ball.State != BallState.Scored)
            return;

        var transform = _world.GetComponent<Transform2D>(_ballEntity);
        _world.AddComponent(_ballEntity, transform with { Position = Vector2.Zero });
        _world.AddComponent(_ballEntity, new Velocity(Vector2.Zero));
        _world.AddComponent(_ballEntity, ball with { State = BallState.Waiting });
    }
}
