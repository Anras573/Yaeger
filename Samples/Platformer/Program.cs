using System.Numerics;
using Platformer.Components;
using Platformer.Systems;
using Yaeger.Audio;
using Yaeger.ECS;
using Yaeger.ECS.Serializers;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Physics;
using Yaeger.Physics.Components;
using Yaeger.Physics.Systems;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

// Platformer sample: the integration proof for the "platformer support" epic, and the flagship
// sample the engine's single-feature 2D demos (Animation2D, CameraDemo, ParticleDemo, SceneDemo,
// UiDemo, OneShotAudioDemo) were absorbed into (#264) — see README.md's feature -> file table.
// A single, complete Super-Mario-like level exercising every feature both epics added, composed
// the same way a real game would: CharacterController2D for the player, a code-built Tilemap with
// merged collision, one-way and moving platforms, patrolling stompable enemies, collectible
// coins, a title screen and pause menu, an HUD, particle effects, pooled one-shot SFX, a
// SceneLoader-driven background, a debug free camera, camera-follow with level bounds, parallax
// backgrounds, sprite flip + an AnimationStateMachine for idle/run/jump/fall (plus one plain,
// non-state-machine Animation on a decorative NPC), streamed music, and keyboard + gamepad input.
//
// Controls: see README.md for the full list.
//
// Out of scope (see the epic issue): multiple levels, save games, power-ups.

using var window = Window.Create();
var world = new World();

var renderer = new Renderer(window);
var fontManager = new FontManager();
var textRenderer = new TextRenderer(window, fontManager);
var font = fontManager.Load("Assets/Roboto-Regular.ttf");
var renderSystem = new UnifiedRenderSystem(renderer, textRenderer, world, window);

var physicsWorld = new PhysicsWorld2D(world);
var characterControllerGravity = new Vector2(0f, -30f);
var characterControllerSystem = new CharacterControllerSystem(world, characterControllerGravity);
var platformPathSystem = new PlatformPathSystem(world);
var cameraFollowSystem = new CameraFollowSystem(world, window);
var parallaxSystem = new ParallaxSystem(world);
var animationSystem = new AnimationSystem(world);
var stateMachineSystem = new AnimationStateMachineSystem(world);
var particleEffects = new ParticleEffectsSystem(world, renderer);
var audioSystem = new AudioSystem(world, window.AudioContext, oneShotVoiceBudget: 16);

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
// Background decorations — not part of the Tiled-less tilemap above, so (per #264) they're
// data-driven from a scene file via SceneLoader/PrefabLoader's shared ComponentRegistry instead
// of code-built: both parallax layers, and a decorative NPC using a plain, non-state-machine
// Animation (contrasted with the player's AnimationStateMachine below) to idle forever.
// ---------------------------------------------------------------------------------------------
var componentRegistry = new ComponentRegistry().RegisterEngineComponents();
var sceneLoader = new SceneLoader(componentRegistry);
var backgroundScene = sceneLoader.Load("Scenes/background.json");
world.Instantiate(backgroundScene);

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

void ResetPlayer()
{
    world.AddComponent(player, new Transform2D(spawnPosition, 0f, playerHalfSize * 2f));
    world.AddComponent(player, Velocity2D.Zero);
    facingLeft = false;
    SetAnimState("idle");
}

// ---------------------------------------------------------------------------------------------
// Coins and enemies — spawn positions are recorded so a full level restart can respawn exactly
// what's been collected/defeated, without touching the (otherwise-static) platforms/goal.
// ---------------------------------------------------------------------------------------------
var coinHalfSize = new Vector2(0.25f, 0.25f);
var coinSpawnPositions = new List<Vector2>();

void SpawnCoin(Vector2 position)
{
    var entity = world.CreateEntity();
    world.AddComponent(entity, new Transform2D(position, 0f, coinHalfSize * 2f));
    world.AddComponent(entity, new Sprite("Assets/coin.png"));
    world.AddComponent(entity, new Coin(coinHalfSize));
    world.AddComponent(entity, new RenderLayer(1));
}

void PlaceCoin(float x, float y)
{
    var position = new Vector2(x, y);
    coinSpawnPositions.Add(position);
    SpawnCoin(position);
}

void RespawnCoins()
{
    foreach (var (entity, _) in world.GetStore<Coin>().All().ToList())
        world.DestroyEntity(entity);
    foreach (var position in coinSpawnPositions)
        SpawnCoin(position);
}

var enemyHalfSize = new Vector2(0.4f, 0.4f);
var enemySpawns = new List<(Vector2 Start, Vector2 End)>();

void SpawnEnemy(Vector2 start, Vector2 end)
{
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

void PlaceEnemyPatrol(int startColumn, int endColumn)
{
    var y = TopOfGroundAt(startColumn) + enemyHalfSize.Y;
    var start = new Vector2(startColumn + 0.5f, y);
    var end = new Vector2(endColumn + 0.5f, y);
    enemySpawns.Add((start, end));
    SpawnEnemy(start, end);
}

void RespawnEnemies()
{
    foreach (var (entity, _) in world.GetStore<Enemy>().All().ToList())
        world.DestroyEntity(entity);
    foreach (var (start, end) in enemySpawns)
        SpawnEnemy(start, end);
}

PlaceEnemyPatrol(16, 22);
PlaceEnemyPatrol(36, 42);

PlaceCoin(16.5f, TopOfGroundAt(16) + 1.5f);
PlaceCoin(18.5f, TopOfGroundAt(18) + 1.9f);
PlaceCoin(20.5f, TopOfGroundAt(20) + 1.5f);
PlaceCoin(52.5f, TopOfGroundAt(52) + 1.3f);

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
PlaceCoin(oneWayColumn - 0.3f, oneWayTopSurfaceY + coinHalfSize.Y + 0.05f);
PlaceCoin(oneWayColumn + 1.3f, oneWayTopSurfaceY + coinHalfSize.Y + 0.05f);

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
// Camera — CameraFollow tracks the player by default; the debug free-cam (see below) toggles it
// off in favour of manual pan/zoom/rotate, the same Camera2D API Samples/CameraDemo demonstrated.
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

var debugCameraSystem = new DebugCameraSystem(world, cameraEntity);

// ---------------------------------------------------------------------------------------------
// UI — title screen, pause menu, and HUD (coins/lives), all built with UiBuilder and owned by
// GameFlowSystem, which also drives the game's state machine (see #264: replaces the old
// Text/TextRenderer-based HUD with the UI system).
// ---------------------------------------------------------------------------------------------
var uiRenderer = new UiRenderer(window);
var gameFlow = new GameFlowSystem(world, window, uiRenderer, textRenderer, font, startingLives: 3);

gameFlow.GameStarted += FullLevelReset;
gameFlow.LevelRestarted += FullLevelReset;
gameFlow.ReturnedToTitle += FullLevelReset;
gameFlow.PlayerRespawned += ResetPlayer;
gameFlow.ExitRequested += window.Close;

void FullLevelReset()
{
    ResetPlayer();
    RespawnCoins();
    RespawnEnemies();
}

// ---------------------------------------------------------------------------------------------
// Audio — music streams continuously; jump/coin/stomp are one-shots routed through AudioSystem's
// pooled voice budget (#264: replaces one dedicated SoundSource per sound, so a burst of nearby
// pickups can never exhaust playback slots).
// ---------------------------------------------------------------------------------------------
var music = StreamingSoundSource.FromFile(window.AudioContext, "Assets/bgm.ogg");
music.Looping = true;
music.Gain = 0.35f;
music.Play();

var jumpBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/jump.wav");
var coinBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/coin.wav");
var stompBuffer = SoundBuffer.FromFile(window.AudioContext, "Assets/stomp.wav");

// ---------------------------------------------------------------------------------------------
// Gameplay wiring, input, and the per-frame update/render loop
// ---------------------------------------------------------------------------------------------
var interactionSystem = new PlayerInteractionSystem(world, player);

interactionSystem.CoinCollected += position =>
{
    gameFlow.CollectCoin();
    particleEffects.SpawnCoinSparkle(position);
    audioSystem.PlayOneShot(coinBuffer, position, gain: 0.8f, AudioGroup.Sfx, priority: 5);
};
interactionSystem.EnemyStomped += position =>
    audioSystem.PlayOneShot(stompBuffer, position, gain: 0.9f, AudioGroup.Sfx, priority: 5);
interactionSystem.PlayerHurt += Die;
interactionSystem.GoalReached += Win;

const float MoveSpeed = 6f;
const float JumpVelocity = 13f;
const float JumpCutMultiplier = 0.45f;

void TryJump()
{
    if (!gameFlow.IsPlaying || debugCameraSystem.IsActive)
        return;
    if (!world.TryGetComponent<CharacterController2D>(player, out var controller))
        return;
    if (!controller.IsGrounded)
        return;

    var velocity = world.GetComponent<Velocity2D>(player);
    velocity.Linear.Y = JumpVelocity;
    world.AddComponent(player, velocity);
    audioSystem.PlayOneShot(
        jumpBuffer,
        world.GetComponent<Transform2D>(player).Position,
        gain: 0.7f,
        AudioGroup.Sfx,
        priority: 3
    );
}

void TryCutJump()
{
    if (!gameFlow.IsPlaying)
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
Keyboard.AddKeyDown(
    Keys.C,
    () =>
    {
        if (gameFlow.IsPlaying)
            debugCameraSystem.Toggle();
    }
);
Gamepad.AddButtonDown(GamepadButton.A, TryJump);
Gamepad.AddButtonUp(GamepadButton.A, TryCutJump);

window.OnUpdate += Update;
window.OnRender += _ =>
{
    renderSystem.Render();
    particleEffects.Render();
    gameFlow.Render();
};
window.OnResize += size => gameFlow.Resize(size);
window.OnClosing += () =>
{
    music.Dispose();
    jumpBuffer.Dispose();
    coinBuffer.Dispose();
    stompBuffer.Dispose();
    audioSystem.Dispose();
    uiRenderer.Dispose();
    textRenderer.Dispose();
    fontManager.Dispose();
    renderer.Dispose();
};

window.Run();
return;

void Update(double deltaTimeD)
{
    var dt = (float)deltaTimeD;

    gameFlow.Update(dt);

    var playerControllable = gameFlow.IsPlaying && !debugCameraSystem.IsActive;
    var worldActive = gameFlow.State != GameState.Paused;

    if (playerControllable)
        HandleInput();

    if (worldActive)
    {
        // PlatformPathSystem sets kinematic Velocity2D; PhysicsWorld2D moves it (and maintains
        // tilemap collision) and must run before CharacterControllerSystem so a rider is carried
        // by the platform's displacement this same step (see CLAUDE.md's moving-platform remarks).
        platformPathSystem.Update(dt);
        physicsWorld.Update(dt);
    }

    if (playerControllable)
    {
        characterControllerSystem.Update(dt);
        interactionSystem.Update();

        if (world.GetComponent<Transform2D>(player).Position.Y < -5f)
            Die();
    }

    if (worldActive)
    {
        if (playerControllable)
            UpdatePlayerAnimation();

        stateMachineSystem.Update(dt);
        animationSystem.Update(dt);

        if (debugCameraSystem.IsActive)
            debugCameraSystem.Update(dt);
        else
            cameraFollowSystem.Update(dt);

        parallaxSystem.Update(dt);

        var controller = world.GetComponent<CharacterController2D>(player);
        var velocity = world.GetComponent<Velocity2D>(player);
        var feetPosition =
            world.GetComponent<Transform2D>(player).Position - new Vector2(0f, playerHalfSize.Y);
        var horizontalSpeed = playerControllable ? velocity.Linear.X : 0f;
        particleEffects.Update(
            dt,
            feetPosition,
            playerControllable && controller.IsGrounded,
            horizontalSpeed
        );
    }

    music.Update();
    audioSystem.Update(dt);
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
    if (!gameFlow.IsPlaying)
        return;

    SetAnimState("dead");
    audioSystem.PlayOneShot(
        stompBuffer,
        world.GetComponent<Transform2D>(player).Position,
        gain: 0.9f,
        AudioGroup.Sfx,
        priority: 8
    );
    gameFlow.Die();
}

void Win()
{
    if (!gameFlow.IsPlaying)
        return;

    gameFlow.Win();
}
