using System.Numerics;
using Pong.Components;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;

namespace Pong;

public class EntityFactory
{
    private readonly World _world;
    private readonly Vector2 _paddleSize = new(0.025f, 0.5f);
    private readonly Sprite _sprite = new("Assets/square.png");
    private readonly Bounds _screenBounds = new()
    {
        MinY = -1.0f,
        MaxY = 1.0f,
        MinX = -1.0f,
        MaxX = 1.0f,
    };

    // Shared paddle prefab built once; Position and Player side are applied as
    // overrides after instantiation.
    private readonly Prefab _paddlePrefab;

    // Ball prefab built once; all ball components at their initial state.
    private readonly Prefab _ballPrefab;

    public EntityFactory(World world)
    {
        _world = world;

        _paddlePrefab = new PrefabBuilder()
            .With(_sprite)
            .With(new Velocity(Vector2.Zero))
            .With(new PlayerControlled())
            .With(_screenBounds with { ClampY = true })
            .With(new PlayerScore(0))
            .Build();

        _ballPrefab = new PrefabBuilder()
            .With(_sprite)
            .With(new Transform2D(Vector2.Zero, 0.0f, new Vector2(0.025f)))
            .With(new Ball { State = BallState.Waiting, Server = Player.Left })
            .With(new Velocity(Vector2.Zero))
            .With(_screenBounds)
            .Build();
    }

    public void SpawnLeftPaddle()
    {
        var leftPaddle = _world.Instantiate(_paddlePrefab, EntityTags.LeftPaddle);
        _world.AddComponent(leftPaddle, new Transform2D(new Vector2(-0.95f, 0), 0.0f, _paddleSize));
        _world.AddComponent(leftPaddle, Player.Left);
    }

    public void SpawnRightPaddle()
    {
        var rightPaddle = _world.Instantiate(_paddlePrefab, EntityTags.RightPaddle);
        _world.AddComponent(rightPaddle, new Transform2D(new Vector2(0.95f, 0), 0.0f, _paddleSize));
        _world.AddComponent(rightPaddle, Player.Right);
    }

    public void SpawnBall()
    {
        _world.Instantiate(_ballPrefab, EntityTags.Ball);
    }

    public void SpawnBackground()
    {
        for (var i = -1f; i < 1f; i += 0.05f)
        {
            var background = _world.CreateEntity();
            _world.AddComponent(background, _sprite);
            _world.AddComponent(
                background,
                new Transform2D(new Vector2(0, i), 0.0f, new Vector2(0.00625f, 0.0125f))
            );
        }
    }

    public void SpawnScoreBoard()
    {
        const int scoreFontSize = 48;
        var fontManager = new FontManager();
        var defaultFont = fontManager.Load("Assets/Roboto-Regular.ttf");

        var leftScore = _world.CreateEntity(EntityTags.LeftScore);
        _world.AddComponent(leftScore, new Text("0", defaultFont, scoreFontSize, Color.White));
        _world.AddComponent(
            leftScore,
            new Transform2D
            {
                Position = new Vector2(-0.15f, 0.8f),
                Scale = new Vector2(0.005f, 0.005f), // Scale down for screen-space rendering
            }
        );

        var rightScore = _world.CreateEntity(EntityTags.RightScore);
        _world.AddComponent(rightScore, new Text("0", defaultFont, scoreFontSize, Color.White));
        _world.AddComponent(
            rightScore,
            new Transform2D
            {
                Position = new Vector2(0.075f, 0.8f),
                Scale = new Vector2(0.005f, 0.005f), // Scale down for screen-space rendering
            }
        );
    }
}
