using System.Numerics;
using Yaeger.Audio;
using Yaeger.Browser;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics.Components;
using Yaeger.Platform;
using Yaeger.Systems;

namespace BrowserDemo;

/// <summary>
/// Owns the ECS world and drives the game loop.  Each tick is invoked by the
/// <c>requestAnimationFrame</c> pump in <see cref="YaegerBrowser.StartGameLoop"/>.
/// </summary>
public sealed class GameController
{
    private readonly World _world;
    private readonly BrowserRenderSurface _renderSurface;
    private readonly IInputState _input = new BrowserInputState();
    private readonly PaddleControlSystem _paddleSystem;
    private readonly BallMovementSystem _movementSystem;
    private readonly BrowserTimeSource _timeSource = new();
    private readonly UnifiedRenderSystem _renderSystem;
    private readonly IAudioOutput _audio;
    private readonly IMusicStream _music;
    private bool _paused;

    /// <summary>Textures the scene draws; preload these before the first <see cref="Tick"/>.</summary>
    public static readonly string[] TexturePaths = [BallTexture];

    private const string BallTexture = "textures/ball.png";

    /// <summary>
    /// Gameplay only sees <see cref="IAudioOutput"/>, so this same code plays its blip and music
    /// loop on a native OpenAL runtime too.
    /// </summary>
    public GameController(
        BrowserRenderSurface renderSurface,
        IAudioOutput audio,
        SoundHandle paddleHitSound
    )
    {
        _renderSurface = renderSurface;
        _audio = audio;
        // Browsers hold audio until the first click/key press; the loop starts then.
        _music = audio.OpenStream("audio/music.mp3");
        _music.Looping = true;
        _music.Gain = 0.5f;
        _music.Play();
        _world = new World();
        // Text goes through the browser's own font stack (Canvas 2D glyph atlas); "sans-serif"
        // is a CSS family, so no font file needs loading.
        _renderSystem = new UnifiedRenderSystem(
            renderSurface,
            new BrowserTextRenderSurface(renderSurface),
            _world,
            renderSurface
        );
        _paddleSystem = new PaddleControlSystem(_world, _input);
        _movementSystem = new BallMovementSystem(
            _world,
            () => audio.Play(paddleHitSound, gain: 0.8f)
        );
        // Opt in: stop the arrow keys from scrolling the page while playing.
        BrowserInputState.SetPreventDefaultKeys([
            Keys.Left,
            Keys.Right,
            Keys.Up,
            Keys.Down,
            Keys.Space,
        ]);
        BuildScene();
    }

    private void BuildScene()
    {
        // A paddle the player drives along the bottom edge...
        var paddle = _world.CreateEntity("paddle");
        _world.AddComponent(
            paddle,
            new Transform2D(new Vector2(0f, -0.85f), scale: new Vector2(0.34f, 0.06f))
        );
        _world.AddComponent(paddle, new Sprite("", new Color(80, 200, 255)));

        // ...and an orange ball that bounces around the canvas, off the paddle when it's there
        // to catch it, or back to serve from the top when it isn't.
        var ball = _world.CreateEntity("ball");
        _world.AddComponent(
            ball,
            new Transform2D(new Vector2(0f, 0.6f), scale: new Vector2(0.08f, 0.08f))
        );
        _world.AddComponent(ball, new Sprite(BallTexture, new Color(255, 255, 255)));
        _world.AddComponent(ball, new Velocity2D(0.45f, -0.6f));

        // A label redrawn every frame; its glyphs come from a cached atlas, so updating the
        // content never allocates new atlas pages.
        var label = _world.CreateEntity("timer");
        _world.AddComponent(label, new Transform2D(new Vector2(-0.95f, 0.88f)));
        _world.AddComponent(
            label,
            new Text("Time 0.0", new FontHandle("sans-serif"), 28, new Color(255, 255, 255))
        );
    }

    // Text lays out in pixels, so scale pixels -> NDC by the canvas size (like UiRenderSystem).
    private void UpdateLabel()
    {
        if (
            !_world.TryGetEntity("timer", out var label)
            || !_world.TryGetComponent<Text>(label, out var text)
            || !_world.TryGetComponent<Transform2D>(label, out var transform)
        )
            return;

        text.Content = $"Time {_timeSource.TotalTime:F1}";
        _world.AddComponent(label, text);

        var size = _renderSurface.Size;
        transform.Scale = new Vector2(2f / MathF.Max(size.X, 1f), 2f / MathF.Max(size.Y, 1f));
        _world.AddComponent(label, transform);
    }

    /// <summary>
    /// Called once per frame by the <c>requestAnimationFrame</c> pump.
    /// The <paramref name="timestampMs"/> is the <c>DOMHighResTimeStamp</c> value from the browser.
    /// </summary>
    public void Tick(double timestampMs)
    {
        _timeSource.Advance(timestampMs);

        // StartGameLoop already began the input frame; this is an idempotent no-op that keeps
        // the controller correct if driven by a different loop.
        BrowserInputState.BeginFrame();

        // One-shot action: WasKeyPressed fires once per press (even a tap shorter than a frame),
        // unlike IsKeyPressed which would toggle every tick the key is held.
        if (_input.WasKeyPressed(Keys.Space))
            _paused = !_paused;

        // M toggles mute through the mixer; it affects the already-playing music immediately.
        if (_input.WasKeyPressed(Keys.M))
            _audio.Mixer.MasterVolume = _audio.Mixer.MasterVolume > 0f ? 0f : 1f;

        if (!_paused)
        {
            _paddleSystem.Update(_timeSource.DeltaTime);
            _movementSystem.Update(_timeSource.DeltaTime);
        }
        _audio.Update(_timeSource.DeltaTime);
        Render();
    }

    private void Render()
    {
        UpdateLabel();
        _renderSystem.Render();
    }
}

/// <summary>
/// Drives the paddle entity from keyboard (arrow keys / A-D) or, while the left mouse button
/// or a touch is held, directly under the pointer — giving desktop and touch players an equally
/// direct way to play.
/// </summary>
internal sealed class PaddleControlSystem(World world, IInputState input) : IUpdateSystem
{
    private const float Speed = 1.6f;

    public void Update(float deltaTime)
    {
        if (
            !world.TryGetEntity("paddle", out var paddle)
            || !world.TryGetComponent<Transform2D>(paddle, out var transform)
        )
            return;

        var halfWidth = transform.Scale.X * 0.5f;
        var x = transform.Position.X;

        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            x = input.MousePositionNdc.X;
        }
        else
        {
            if (input.IsKeyPressed(Keys.Left) || input.IsKeyPressed(Keys.A))
                x -= Speed * deltaTime;
            if (input.IsKeyPressed(Keys.Right) || input.IsKeyPressed(Keys.D))
                x += Speed * deltaTime;
        }

        var clampedX = Math.Clamp(x, -1f + halfWidth, 1f - halfWidth);
        transform.Position = new Vector2(clampedX, transform.Position.Y);
        world.AddComponent(paddle, transform);
    }
}

/// <summary>
/// Moves the ball via its <see cref="Velocity2D"/>, bouncing it off the side/top NDC edges
/// (±1) and off the paddle when it's there to catch it. A ball that gets past the paddle is
/// re-served from the top rather than ending the game — this is a bounce-practice toy, not a
/// scored game.
/// </summary>
internal sealed class BallMovementSystem(World world, Action? onPaddleHit = null) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        // Declared outside the && so it's definitely assigned even when the short-circuit
        // skips TryGetComponent (the compiler can't otherwise prove that from `hasPaddle` alone
        // once it's read back later in a separate `if`).
        var paddleTransform = default(Transform2D);
        var hasPaddle =
            world.TryGetEntity("paddle", out var paddleEntity)
            && world.TryGetComponent(paddleEntity, out paddleTransform);

        foreach (var (entity, velocity, transform) in world.Query<Velocity2D, Transform2D>())
        {
            var pos = transform.Position;
            var vel = velocity.Linear;
            var halfScale = transform.Scale * 0.5f;

            pos += vel * deltaTime;

            // Reflect off the side and top NDC edges (±1) and clamp to prevent tunnelling.
            if (pos.X - halfScale.X < -1f || pos.X + halfScale.X > 1f)
            {
                vel.X = -vel.X;
                pos.X = Math.Clamp(pos.X, -1f + halfScale.X, 1f - halfScale.X);
            }

            if (pos.Y + halfScale.Y > 1f)
            {
                vel.Y = -vel.Y;
                pos.Y = 1f - halfScale.Y;
            }

            // Bounce off the paddle: reflect upward and nudge the X velocity based on where it
            // was hit, Breakout-style, so the player can aim the return.
            if (hasPaddle && vel.Y < 0f)
            {
                var paddleHalf = paddleTransform.Scale * 0.5f;
                var paddlePos = paddleTransform.Position;
                var overlapsX =
                    pos.X + halfScale.X > paddlePos.X - paddleHalf.X
                    && pos.X - halfScale.X < paddlePos.X + paddleHalf.X;
                var paddleTop = paddlePos.Y + paddleHalf.Y;

                if (overlapsX && pos.Y - halfScale.Y <= paddleTop && pos.Y >= paddlePos.Y)
                {
                    vel.Y = -vel.Y;
                    pos.Y = paddleTop + halfScale.Y;
                    vel.X += (pos.X - paddlePos.X) / paddleHalf.X * 0.5f;
                    onPaddleHit?.Invoke();
                }
            }

            // Missed the paddle: serve a fresh ball back in from the top.
            if (pos.Y + halfScale.Y < -1f)
            {
                pos = new Vector2(0f, 0.6f);
                vel = new Vector2(vel.X >= 0f ? 0.45f : -0.45f, -0.6f);
            }

            var newTransform = transform;
            newTransform.Position = pos;
            world.AddComponent(entity, newTransform);

            var newVelocity = velocity;
            newVelocity.Linear = vel;
            world.AddComponent(entity, newVelocity);
        }
    }
}
