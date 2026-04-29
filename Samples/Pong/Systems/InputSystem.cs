using System.Numerics;
using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Systems;

namespace Pong.Systems;

public class InputSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        HandleBallInput();

        foreach (
            (Entity paddle, Transform2D transform, Velocity velocity, _) in world.Query<
                Transform2D,
                Velocity,
                PlayerControlled
            >()
        )
        {
            world.AddComponent(paddle, HandlePaddleInput(transform, velocity));
        }
    }

    private static Velocity HandlePaddleInput(Transform2D transform, Velocity velocity)
    {
        // Naive paddle detection based on x position
        var (upKey, downKey) = transform.Position.X < 0f ? (Keys.W, Keys.S) : (Keys.Up, Keys.Down);

        var dir = 0f;
        if (Keyboard.IsKeyPressed(upKey))
            dir += 1f;
        if (Keyboard.IsKeyPressed(downKey))
            dir -= 1f;

        velocity.Value = new Vector2(0, dir);
        return velocity;
    }

    private void HandleBallInput()
    {
        var ballEntity = world.GetEntity(EntityTags.Ball);
        var ball = world.GetComponent<Ball>(ballEntity);

        if (ball.State != BallState.Waiting)
            return;

        // If the ball is stopped, we can use the space key to start it
        if (!Keyboard.IsKeyPressed(Keys.Space))
            return;

        var dirX = ball.Server == Player.Left ? 0.5f : -0.5f;
        var dirY = Random.Shared.NextSingle();

        ball.State = BallState.Moving;
        world.AddComponent(ballEntity, ball);
        world.AddComponent(ballEntity, new Velocity(new Vector2(dirX, dirY)));
    }
}
