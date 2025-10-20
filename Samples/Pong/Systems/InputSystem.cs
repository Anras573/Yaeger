using System.Numerics;

using Pong.Components;

using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;

namespace Pong.Systems;

public class InputSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        HandleBallInput();

        foreach ((Entity paddle, Transform2D transform, Velocity velocity, _) in world.Query<Transform2D, Velocity, PlayerControlled>())
        {
            world.AddComponent(paddle, HandlePaddleInput(transform, velocity));
        }
    }

    private static Velocity HandlePaddleInput(Transform2D transform, Velocity velocity)
    {
        var dir = 0f;
        switch (transform.Position.X)
        {
            // Naive paddle detection based on x position
            // Left paddle
            case < 0f:
                {
                    if (Keyboard.IsKeyPressed(Keys.W)) dir += 1f;
                    if (Keyboard.IsKeyPressed(Keys.S)) dir -= 1f;
                    velocity.Value = new Vector2(0, dir * 1.0f);
                    break;
                }
            // Right paddle
            case > 0f:
                {
                    if (Keyboard.IsKeyPressed(Keys.Up)) dir += 1f;
                    if (Keyboard.IsKeyPressed(Keys.Down)) dir -= 1f;
                    velocity.Value = new Vector2(0, dir * 1.0f);
                    break;
                }
        }

        return velocity;
    }

    private void HandleBallInput()
    {
        (Entity ballEntity, Ball ball) = world.GetStore<Ball>().All().First();
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