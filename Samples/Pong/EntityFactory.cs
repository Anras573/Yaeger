using System.Numerics;

using Pong.Components;

using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;

namespace Pong;

public class EntityFactory(World world)
{
    private readonly Vector2 _paddleSize = new(0.025f, 0.5f);
    private readonly Sprite _sprite = new("Assets/square.png");
    private readonly Bounds _screenBounds = new()
    {
        MinY = -1.0f,
        MaxY = 1.0f,
        MinX = -1.0f,
        MaxX = 1.0f
    };

    public void SpawnLeftPaddle()
    {
        var leftPaddle = world.CreateEntity(EntityTags.LeftPaddle);
        world.AddComponent(leftPaddle, _sprite);
        world.AddComponent(leftPaddle, new Transform2D(new Vector2(-0.95f, 0), 0.0f, _paddleSize));
        world.AddComponent(leftPaddle, new Velocity(Vector2.Zero));
        world.AddComponent(leftPaddle, new PlayerControlled());
        world.AddComponent(leftPaddle, _screenBounds with { ClampY = true });
        world.AddComponent(leftPaddle, new PlayerScore(0));
        world.AddComponent(leftPaddle, Player.Left);
    }

    public void SpawnRightPaddle()
    {
        var rightPaddle = world.CreateEntity(EntityTags.RightPaddle);
        world.AddComponent(rightPaddle, _sprite);
        world.AddComponent(rightPaddle, new Transform2D(new Vector2(0.95f, 0), 0.0f, _paddleSize));
        world.AddComponent(rightPaddle, new Velocity(Vector2.Zero));
        world.AddComponent(rightPaddle, new PlayerControlled());
        world.AddComponent(rightPaddle, _screenBounds with { ClampY = true });
        world.AddComponent(rightPaddle, new PlayerScore(0));
        world.AddComponent(rightPaddle, Player.Right);
    }

    public void SpawnBall()
    {
        var ball = world.CreateEntity(EntityTags.Ball);
        world.AddComponent(ball, _sprite);
        world.AddComponent(ball, new Transform2D(Vector2.Zero, 0.0f, new Vector2(0.025f)));
        world.AddComponent(ball, new Ball
        {
            State = BallState.Waiting,
            Server = Player.Left
        });
        world.AddComponent(ball, new Velocity(Vector2.Zero));
        world.AddComponent(ball, _screenBounds);
    }

    public void SpawnBackground()
    {
        for (var i = -1f; i < 1f; i += 0.05f)
        {
            var background = world.CreateEntity();
            world.AddComponent(background, _sprite);
            world.AddComponent(background, new Transform2D(new Vector2(0, i), 0.0f, new Vector2(0.00625f, 0.0125f)));
        }
    }

    public void SpawnScoreBoard()
    {
        const int scoreFontSize = 48;
        var fontManager = new FontManager();
        var defaultFont = fontManager.Load("Assets/Roboto-Regular.ttf");

        var leftScore = world.CreateEntity(EntityTags.LeftScore);
        world.AddComponent(leftScore, new Text("0", defaultFont, scoreFontSize, Color.White));
        world.AddComponent(leftScore, new Transform2D
        {
            Position = new Vector2(-0.15f, 0.8f),
            Scale = new Vector2(0.005f, 0.005f) // Scale down for screen-space rendering
        });

        var rightScore = world.CreateEntity(EntityTags.RightScore);
        world.AddComponent(rightScore, new Text("0", defaultFont, scoreFontSize, Color.White));
        world.AddComponent(rightScore, new Transform2D
        {
            Position = new Vector2(0.075f, 0.8f),
            Scale = new Vector2(0.005f, 0.005f) // Scale down for screen-space rendering
        });
    }
}