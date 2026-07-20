using System.Numerics;
using Platformer.Components;
using Platformer.Systems;
using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Platformer sample: the integration proof for the "platformer support" epic. A single,
// complete Super-Mario-like level exercising every feature that epic added, composed the same
// way a real game would: CharacterController2D for the player, a code-built Tilemap with merged
// collision, one-way and moving platforms, patrolling stompable enemies, collectible coins, a
// camera-follow with level bounds, parallax backgrounds, sprite flip + an AnimationStateMachine
// for idle/run/jump/fall, streamed music + SFX, and keyboard + gamepad input.
//
// Controls:
//   A/D or ←/→   — move
//   Space or ↑   — jump (hold longer for a higher jump; release early to cut it short)
//   R            — restart after dying or winning
//   ESC          — exit
//   Gamepad      — left stick / D-pad to move, A to jump, Start to restart
//
// Out of scope (see the epic issue): multiple levels, menus, save games, power-ups.

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var fontManager = new FontManager();
var textRenderer = new TextRenderer(window);
var renderSystem = new UnifiedRenderSystem(renderer, textRenderer, world, window);

var physicsWorld = new PhysicsWorld2D(world);
var characterControllerGravity = new Vector2(0f, -30f);
var characterControllerSystem = new CharacterControllerSystem(world, characterControllerGravity);
var platformPathSystem = new PlatformPathSystem(world);
var cameraFollowSystem = new CameraFollowSystem(world, window);
var parallaxSystem = new ParallaxSystem(world);
var animationSystem = new AnimationSystem(world);
var stateMachineSystem = new AnimationStateMachineSystem(world);

// ---------------------------------------------------------------------------------------------
// Level: a code-built tilemap. Two tile types (both solid): grass-top ground and brick.
// Rows are top-to-bottom; row (LevelHeight - 1) is the map's bottom row.
// ---------------------------------------------------------------------------------------------
const int LevelWidth = 56;
const int LevelHeight = 14;
const int GroundRow = LevelHeight - 1;
const int GroundRow2 = LevelHeight - 2;

var tileset = new Tileset("Assets/tileset.png", columns: 2, rows: 1, solidTileIndices: [0, 1]);
var tiles = new int[LevelWidth * LevelHeight];
Array.Fill(tiles, Tilemap.EmptyTile);

void SetTile(int column, int row, int tileIndex) => tiles[row * LevelWidth + column] = tileIndex;

float TopOfGroundAt(int column)
{
    for (var row = 0; row < LevelHeight; row++)
    {
        if (tiles[row * LevelWidth + column] != Tilemap.EmptyTile)
            return LevelHeight - 1 - row + 1f;
    }
    return 0f;
}

// Ground segments, with two pits in between: one plain gap (jump it directly) and one bridged
// only by the moving platform.
(int Start, int End)[] groundSegments = [(0, 9), (13, 25), (33, 45)];
foreach (var (start, end) in groundSegments)
{
    for (var c = start; c <= end; c++)
    {
        SetTile(c, GroundRow, 0);
        SetTile(c, GroundRow2, 0);
    }
}

// Ascending brick staircase (columns 46-48) up to the raised goal plateau (columns 49-55) — a
// pit underneath means falling off the staircase or the plateau means falling to your death.
SetTile(46, 11, 1);
SetTile(47, 10, 1);
SetTile(48, 9, 1);
for (var c = 49; c <= 55; c++)
{
    SetTile(c, 9, 1);
    SetTile(c, 8, 1);
}

var tilemap = new Tilemap(tileset, LevelWidth, LevelHeight, tiles);
var tilemapEntity = world.CreateEntity("level");
var tilemapTransform = new Transform2D(Vector2.Zero);
world.AddComponent(tilemapEntity, tilemapTransform);
world.AddComponent(tilemapEntity, tilemap);
world.AddComponent(tilemapEntity, new RenderLayer(0));

// ---------------------------------------------------------------------------------------------
// Player
// ---------------------------------------------------------------------------------------------
var playerHalfSize = new Vector2(0.5f, 0.5f);
var spawnPosition = new Vector2(2.5f, TopOfGroundAt(2) + playerHalfSize.Y);

var sheets = new Dictionary<string, SpriteSheet>
{
    ["idle"] = new SpriteSheet("Assets/Idle.png", columns: 6),
    ["run"] = new SpriteSheet("Assets/Run.png", columns: 8),
    ["jump"] = new SpriteSheet("Assets/Jump.png", columns: 12),
    ["fall"] = new SpriteSheet("Assets/Jump.png", columns: 12),
    ["dead"] = new SpriteSheet("Assets/Dead.png", columns: 3),
};

Animation MakeClip(int frameCount, float duration, bool loop) =>
    new(
        Enumerable
            .Range(0, frameCount)
            .Select(_ => new AnimationFrame("_unused_", duration))
            .ToArray(),
        loop
    );

var animationStates = new Dictionary<string, Animation>
{
    ["idle"] = MakeClip(sheets["idle"].FrameCount, 0.12f, loop: true),
    ["run"] = MakeClip(sheets["run"].FrameCount, 0.07f, loop: true),
    ["jump"] = MakeClip(sheets["jump"].FrameCount, 0.05f, loop: false),
    ["fall"] = MakeClip(sheets["fall"].FrameCount, 0.05f, loop: false),
    ["dead"] = MakeClip(sheets["dead"].FrameCount, 0.2f, loop: false),
};

var player = world.CreateEntity("player");
world.AddComponent(player, new Transform2D(spawnPosition, 0f, playerHalfSize * 2f));
world.AddComponent(
    player,
    new CharacterController2D(playerHalfSize * 2f, stepHeight: 0.3f, gravityScale: 1f)
);
world.AddComponent(player, Velocity2D.Zero);
world.AddComponent(player, sheets["idle"]);
world.AddComponent(player, new AnimationState());
world.AddComponent(player, new AnimationStateMachine(animationStates, "idle"));
world.AddComponent(player, new Sprite("_flip_carrier_"));

var currentAnimState = "idle";
var facingLeft = false;

void SetAnimState(string name)
{
    if (name == currentAnimState)
        return;

    currentAnimState = name;
    world.AddComponent(player, sheets[name]);
    stateMachineSystem.Play(player, name);
}

// ---------------------------------------------------------------------------------------------
// Coins
// ---------------------------------------------------------------------------------------------
var coinHalfSize = new Vector2(0.25f, 0.25f);

void CreateCoin(float x, float y)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, new Transform2D(new Vector2(x, y), 0f, coinHalfSize * 2f));
    world.AddComponent(entity, new Sprite("Assets/coin.png"));
    world.AddComponent(entity, new Coin(coinHalfSize));
    world.AddComponent(entity, new RenderLayer(1));
}

// ---------------------------------------------------------------------------------------------
// Enemies — kinematic bodies patrolling via PlatformPath, trigger colliders so they never
// physically block the player (CharacterControllerSystem.SolidCandidates skips triggers);
// PlayerInteractionSystem decides stomp vs. damage.
// ---------------------------------------------------------------------------------------------
var enemyHalfSize = new Vector2(0.4f, 0.4f);

void CreateEnemyPatrol(int startColumn, int endColumn)
{
    var y = TopOfGroundAt(startColumn) + enemyHalfSize.Y;
    var start = new Vector2(startColumn + 0.5f, y);
    var end = new Vector2(endColumn + 0.5f, y);

    var entity = world.CreateEntity();
    world.AddComponent(entity, new Transform2D(start, 0f, enemyHalfSize * 2f));
    world.AddComponent(entity, new Sprite("Assets/enemy.png"));
    world.AddComponent(entity, RigidBody2D.CreateKinematic());
    world.AddComponent(entity, Velocity2D.Zero);
    world.AddComponent(entity, new BoxCollider2D(enemyHalfSize * 2f, isTrigger: true));
    world.AddComponent(entity, new PlatformPath([start, end], speed: 1.5f, pingPong: true));
    world.AddComponent(entity, new Enemy(enemyHalfSize));
    world.AddComponent(entity, new RenderLayer(1));
}

CreateEnemyPatrol(16, 22);
CreateEnemyPatrol(36, 42);

CreateCoin(16.5f, TopOfGroundAt(16) + 1.5f);
CreateCoin(18.5f, TopOfGroundAt(18) + 1.9f);
CreateCoin(20.5f, TopOfGroundAt(20) + 1.5f);
CreateCoin(52.5f, TopOfGroundAt(52) + 1.3f);

// ---------------------------------------------------------------------------------------------
// One-way platform — jump up through it from below, land on top. Sits above ground segment 3,
// with coins on top that require using it.
// ---------------------------------------------------------------------------------------------
var oneWayHalfSize = new Vector2(1.2f, 0.15f);
var oneWayColumn = 38;
var oneWayCenter = new Vector2(
    oneWayColumn + 0.5f,
    TopOfGroundAt(oneWayColumn) + 2.2f + oneWayHalfSize.Y
);

var oneWayEntity = world.CreateEntity();
world.AddComponent(oneWayEntity, new Transform2D(oneWayCenter, 0f, oneWayHalfSize * 2f));
world.AddComponent(oneWayEntity, new Sprite("Assets/platform.png"));
world.AddComponent(oneWayEntity, new BoxCollider2D(oneWayHalfSize * 2f, oneWay: true));
world.AddComponent(oneWayEntity, new RenderLayer(1));

var oneWayTopSurfaceY = oneWayCenter.Y + oneWayHalfSize.Y;
CreateCoin(oneWayColumn - 0.3f, oneWayTopSurfaceY + coinHalfSize.Y + 0.05f);
CreateCoin(oneWayColumn + 1.3f, oneWayTopSurfaceY + coinHalfSize.Y + 0.05f);

// ---------------------------------------------------------------------------------------------
// Moving platform — bridges the second pit (columns 26-32), carrying the player across via
// CharacterControllerSystem's rider-carrying (see CLAUDE.md's "Moving platforms" remarks).
// ---------------------------------------------------------------------------------------------
const int PitStart = 26;
const int PitEnd = 32;
var movingPlatformHalfSize = new Vector2(1.5f, 0.15f);
var movingPlatformY = TopOfGroundAt(PitStart - 1) - movingPlatformHalfSize.Y;

// Endpoints chosen so the platform's edge touches solid ground at either extreme, making it a
// seamless bridge across the pit in both resting positions.
var movingPlatformFrom = new Vector2(PitStart + movingPlatformHalfSize.X, movingPlatformY);
var movingPlatformTo = new Vector2(PitEnd + 1f - movingPlatformHalfSize.X, movingPlatformY);

var movingPlatform = world.CreateEntity();
world.AddComponent(
    movingPlatform,
    new Transform2D(movingPlatformFrom, 0f, movingPlatformHalfSize * 2f)
);
world.AddComponent(movingPlatform, new Sprite("Assets/platform.png"));
world.AddComponent(movingPlatform, RigidBody2D.CreateKinematic());
world.AddComponent(movingPlatform, Velocity2D.Zero);
world.AddComponent(movingPlatform, new BoxCollider2D(movingPlatformHalfSize * 2f));
world.AddComponent(
    movingPlatform,
    new PlatformPath([movingPlatformFrom, movingPlatformTo], speed: 2.5f, pingPong: true)
);
world.AddComponent(movingPlatform, new RenderLayer(1));

// ---------------------------------------------------------------------------------------------
// Goal flag
// ---------------------------------------------------------------------------------------------
var goalHalfSize = new Vector2(0.3f, 0.6f);
const int GoalColumn = 54;
var goalEntity = world.CreateEntity();
world.AddComponent(
    goalEntity,
    new Transform2D(
        new Vector2(GoalColumn + 0.5f, TopOfGroundAt(GoalColumn) + goalHalfSize.Y),
        0f,
        goalHalfSize * 2f
    )
);
world.AddComponent(goalEntity, new Sprite("Assets/flag.png"));
world.AddComponent(goalEntity, new Goal(goalHalfSize));
world.AddComponent(goalEntity, new RenderLayer(1));

// ---------------------------------------------------------------------------------------------
// Camera + parallax background
// ---------------------------------------------------------------------------------------------
var cameraEntity = world.CreateEntity("camera");
world.AddComponent(cameraEntity, new Camera2D(spawnPosition, Zoom: 0.11f));
world.AddComponent(
    cameraEntity,
    new CameraFollow(
        player,
        smoothing: 6f,
        deadzoneHalfExtents: new Vector2(1.5f, 1f),
        lookAheadTime: 0.15f
    )
);
world.AddComponent(cameraEntity, CameraBounds.FromTilemap(tilemap, tilemapTransform));

var skyCenter = new Vector2(LevelWidth / 2f, LevelHeight / 2f);
var skyEntity = world.CreateEntity();
world.AddComponent(skyEntity, new Sprite("Assets/parallax_sky.png"));
world.AddComponent(
    skyEntity,
    new Transform2D(skyCenter, 0f, new Vector2(LevelWidth * 2.2f, LevelHeight * 2.2f))
);
world.AddComponent(
    skyEntity,
    new ParallaxLayer(scrollFactorX: 0.05f, scrollFactorY: 0f) { BasePosition = skyCenter }
);
world.AddComponent(skyEntity, new RenderLayer(-2));

var hillsCenter = new Vector2(LevelWidth / 2f, 3f);
var hillsEntity = world.CreateEntity();
world.AddComponent(hillsEntity, new Sprite("Assets/parallax_hills.png"));
world.AddComponent(
    hillsEntity,
    new Transform2D(hillsCenter, 0f, new Vector2(LevelWidth * 1.6f, 8f))
);
world.AddComponent(
    hillsEntity,
    new ParallaxLayer(scrollFactorX: 0.3f, scrollFactorY: 0f) { BasePosition = hillsCenter }
);
world.AddComponent(hillsEntity, new RenderLayer(-1));

// ---------------------------------------------------------------------------------------------
// HUD
// ---------------------------------------------------------------------------------------------
var font = fontManager.Load("Assets/Roboto-Regular.ttf");

var hudCoinsEntity = world.CreateEntity("hud-coins");
world.AddComponent(hudCoinsEntity, new Text("Coins: 0", font, 22, Color.White));
world.AddComponent(
    hudCoinsEntity,
    new Transform2D { Position = new Vector2(-0.95f, 0.9f), Scale = new Vector2(0.0035f) }
);

var hudControlsEntity = world.CreateEntity("hud-controls");
world.AddComponent(
    hudControlsEntity,
    new Text(
        "A/D or arrows: move   Space/Up: jump   R: restart   Gamepad: stick + A",
        font,
        14,
        Color.White
    )
);
world.AddComponent(
    hudControlsEntity,
    new Transform2D { Position = new Vector2(-0.95f, -0.9f), Scale = new Vector2(0.0028f) }
);

var hudMessageEntity = world.CreateEntity("hud-message");
world.AddComponent(hudMessageEntity, new Text("", font, 36, new Color(255, 220, 0)));
world.AddComponent(
    hudMessageEntity,
    new Transform2D { Position = new Vector2(-0.55f, 0.05f), Scale = new Vector2(0.0045f) }
);

void SetMessage(string text) =>
    world.AddComponent(hudMessageEntity, new Text(text, font, 36, new Color(255, 220, 0)));

// ---------------------------------------------------------------------------------------------
// Audio
// ---------------------------------------------------------------------------------------------
var music = StreamingSoundSource.FromFile(window.AudioContext, "Assets/bgm.ogg");
music.Looping = true;
music.Gain = 0.35f;
music.Play();

var jumpSfx = SoundSource.Create(window.AudioContext, AudioGroup.Sfx);
jumpSfx.SetBuffer(SoundBuffer.FromFile(window.AudioContext, "Assets/jump.wav"));

var coinSfx = SoundSource.Create(window.AudioContext, AudioGroup.Sfx);
coinSfx.SetBuffer(SoundBuffer.FromFile(window.AudioContext, "Assets/coin.wav"));

var stompSfx = SoundSource.Create(window.AudioContext, AudioGroup.Sfx);
stompSfx.SetBuffer(SoundBuffer.FromFile(window.AudioContext, "Assets/stomp.wav"));

// ---------------------------------------------------------------------------------------------
// Game state, input, and the per-frame update/render loop
// ---------------------------------------------------------------------------------------------
var interactionSystem = new PlayerInteractionSystem(world, player);
var score = 0;

var state = GameState.Playing;

interactionSystem.CoinCollected += () =>
{
    score++;
    world.AddComponent(hudCoinsEntity, new Text($"Coins: {score}", font, 22, Color.White));
    coinSfx.Play();
};
interactionSystem.EnemyStomped += () => stompSfx.Play();
interactionSystem.PlayerHurt += Die;
interactionSystem.GoalReached += Win;

const float MoveSpeed = 6f;
const float JumpVelocity = 13f;
const float JumpCutMultiplier = 0.45f;

void TryJump()
{
    if (state != GameState.Playing)
        return;
    if (!world.TryGetComponent<CharacterController2D>(player, out var controller))
        return;
    if (!controller.IsGrounded)
        return;

    var velocity = world.GetComponent<Velocity2D>(player);
    velocity.Linear.Y = JumpVelocity;
    world.AddComponent(player, velocity);
    jumpSfx.Play();
}

void TryCutJump()
{
    if (state != GameState.Playing)
        return;

    var velocity = world.GetComponent<Velocity2D>(player);
    if (velocity.Linear.Y > 0f)
    {
        velocity.Linear.Y *= JumpCutMultiplier;
        world.AddComponent(player, velocity);
    }
}

Keyboard.AddKeyDown(Keys.Escape, window.Close);
Keyboard.AddKeyDown(Keys.Space, TryJump);
Keyboard.AddKeyUp(Keys.Space, TryCutJump);
Keyboard.AddKeyDown(Keys.Up, TryJump);
Keyboard.AddKeyUp(Keys.Up, TryCutJump);
Keyboard.AddKeyDown(Keys.R, TryRestart);
Gamepad.AddButtonDown(GamepadButton.A, TryJump);
Gamepad.AddButtonUp(GamepadButton.A, TryCutJump);
Gamepad.AddButtonDown(GamepadButton.Start, TryRestart);

window.OnUpdate += Update;
window.OnRender += _ => renderSystem.Render();
window.OnClosing += () =>
{
    music.Dispose();
    jumpSfx.Dispose();
    coinSfx.Dispose();
    stompSfx.Dispose();
    textRenderer.Dispose();
    fontManager.Dispose();
    renderer.Dispose();
};

window.Run();
return;

void Update(double deltaTimeD)
{
    var dt = (float)deltaTimeD;

    if (state == GameState.Playing)
        HandleInput();

    // PlatformPathSystem sets kinematic Velocity2D; PhysicsWorld2D moves it (and maintains
    // tilemap collision) and must run before CharacterControllerSystem so a rider is carried
    // by the platform's displacement this same step (see CLAUDE.md's moving-platform remarks).
    platformPathSystem.Update(dt);
    physicsWorld.Update(dt);

    if (state == GameState.Playing)
    {
        characterControllerSystem.Update(dt);
        interactionSystem.Update();

        if (world.GetComponent<Transform2D>(player).Position.Y < -5f)
            Die();
    }

    if (state == GameState.Playing)
        UpdatePlayerAnimation();

    stateMachineSystem.Update(dt);
    animationSystem.Update(dt);
    cameraFollowSystem.Update(dt);
    parallaxSystem.Update(dt);
    music.Update();
}

void HandleInput()
{
    var moveInput = 0f;
    if (Keyboard.IsKeyPressed(Keys.A) || Keyboard.IsKeyPressed(Keys.Left))
        moveInput -= 1f;
    if (Keyboard.IsKeyPressed(Keys.D) || Keyboard.IsKeyPressed(Keys.Right))
        moveInput += 1f;

    var stickX = Gamepad.LeftStick.X;
    if (MathF.Abs(stickX) > 0.01f)
        moveInput = stickX;
    if (Gamepad.IsButtonPressed(GamepadButton.DPadLeft))
        moveInput = -1f;
    if (Gamepad.IsButtonPressed(GamepadButton.DPadRight))
        moveInput = 1f;

    moveInput = Math.Clamp(moveInput, -1f, 1f);

    var velocity = world.GetComponent<Velocity2D>(player);
    velocity.Linear.X = moveInput * MoveSpeed;
    world.AddComponent(player, velocity);

    if (moveInput < -0.01f)
        facingLeft = true;
    else if (moveInput > 0.01f)
        facingLeft = false;
}

void UpdatePlayerAnimation()
{
    var controller = world.GetComponent<CharacterController2D>(player);
    var velocity = world.GetComponent<Velocity2D>(player);

    var next = controller.IsGrounded
        ? (MathF.Abs(velocity.Linear.X) > 0.05f ? "run" : "idle")
        : (velocity.Linear.Y > 0f ? "jump" : "fall");

    SetAnimState(next);
    world.AddComponent(player, new Sprite("_flip_carrier_", flipX: facingLeft));
}

void Die()
{
    if (state != GameState.Playing)
        return;

    state = GameState.Dead;
    SetAnimState("dead");
    stompSfx.Play();
    SetMessage("You died! Press R to restart");
}

void Win()
{
    if (state != GameState.Playing)
        return;

    state = GameState.Won;
    SetMessage("You win! Press R to restart");
}

void TryRestart()
{
    if (state == GameState.Playing)
        return;

    var transform = world.GetComponent<Transform2D>(player);
    transform.Position = spawnPosition;
    world.AddComponent(player, transform);
    world.AddComponent(player, Velocity2D.Zero);
    SetAnimState("idle");
    SetMessage("");
    state = GameState.Playing;
}

enum GameState
{
    Playing,
    Dead,
    Won,
}
