using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Platform;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.UI;
using Yaeger.Windowing;

namespace Platformer.Systems;

public enum GameState
{
    Title,
    Playing,
    Paused,
    Dead,
    Won,
}

/// <summary>
/// Owns the title screen, pause menu, and HUD (all built with <see cref="UiBuilder"/>) plus the
/// state machine that ties them together — score, lives, pause/resume, restart, and returning to
/// the title screen. Input for state transitions (P/Start to pause, R/Start to restart) is bound
/// here so <c>Program.cs</c> only has to construct this once, call <see cref="Update"/>/
/// <see cref="Render"/> each frame, and react to its events to reset gameplay state (player
/// position, coins) it doesn't itself know about.
/// </summary>
public sealed class GameFlowSystem
{
    private readonly World _world;
    private readonly UiSystem _uiSystem;
    private readonly UiRenderSystem _uiRenderSystem;
    private readonly int _startingLives;
    private readonly List<Entity> _overlayEntities = [];
    private readonly List<Entity> _hudEntities = [];

    private Vector2 _windowSize;
    private bool _gameOverPending;
    private bool _hudBuilt;
    private Entity _hudCoinsLabel;
    private Entity _hudLivesLabel;
    private Entity _hudMessageLabel;

    public GameFlowSystem(
        World world,
        Window window,
        UiRenderer uiRenderer,
        ITextRenderSurface textRenderer,
        IFontHandle font,
        int startingLives = 3
    )
    {
        _world = world;
        _startingLives = startingLives;
        Lives = startingLives;
        _windowSize = window.Size;

        _uiSystem = new UiSystem(world);
        _uiRenderSystem = new UiRenderSystem(world, uiRenderer, textRenderer, font, window);

        ShowTitleMenu();

        Keyboard.AddKeyDown(Keys.P, HandleStartButton);
        Keyboard.AddKeyDown(Keys.R, HandleRestartKey);
        Gamepad.AddButtonDown(GamepadButton.Start, HandleStartButton);
    }

    public GameState State { get; private set; } = GameState.Title;
    public int Score { get; private set; }
    public int Lives { get; private set; }

    /// <summary>True only while gameplay systems should simulate and accept player input.</summary>
    public bool IsPlaying => State == GameState.Playing;

    /// <summary>Raised when Play is pressed from the title screen — reset gameplay to a fresh start.</summary>
    public event Action? GameStarted;

    /// <summary>Raised by the pause menu's Restart — reset gameplay (coins, position) to a fresh start.</summary>
    public event Action? LevelRestarted;

    /// <summary>Raised after dying with lives remaining — reposition the player only, keep score/coins.</summary>
    public event Action? PlayerRespawned;

    /// <summary>Raised when returning to the title screen (pause menu, or a game-over restart).</summary>
    public event Action? ReturnedToTitle;

    /// <summary>Raised when Quit is pressed from the title screen.</summary>
    public event Action? ExitRequested;

    public void Update(float deltaTime)
    {
        _uiSystem.Update(deltaTime);

        switch (State)
        {
            case GameState.Title:
                if (WasClicked("btn-play"))
                    StartGame();
                else if (WasClicked("btn-quit-title"))
                    ExitRequested?.Invoke();
                break;

            case GameState.Paused:
                if (WasClicked("btn-resume"))
                    Resume();
                else if (WasClicked("btn-restart"))
                    RestartLevel();
                else if (WasClicked("btn-quit-to-title"))
                    GoToTitle();
                break;
        }
    }

    public void Render() => _uiRenderSystem.Render();

    public void Resize(Vector2 windowSize)
    {
        _windowSize = windowSize;

        switch (State)
        {
            case GameState.Title:
                ShowTitleMenu();
                break;
            case GameState.Paused:
                ShowPauseMenu();
                break;
        }

        if (_hudBuilt)
            RebuildHud();
    }

    public void CollectCoin()
    {
        Score++;
        UpdateHudCoins();
    }

    public void Die()
    {
        if (State != GameState.Playing)
            return;

        Lives--;
        UpdateHudLives();

        if (Lives <= 0)
        {
            _gameOverPending = true;
            ShowMessage("Game Over! Press R for the title screen");
        }
        else
        {
            ShowMessage("You died! Press R to respawn");
        }

        State = GameState.Dead;
    }

    public void Win()
    {
        if (State != GameState.Playing)
            return;

        ShowMessage("You win! Press R to play again");
        State = GameState.Won;
    }

    private void HandleStartButton()
    {
        switch (State)
        {
            case GameState.Title:
                StartGame();
                break;
            case GameState.Playing:
                Pause();
                break;
            case GameState.Paused:
                Resume();
                break;
            case GameState.Dead:
            case GameState.Won:
                HandleRestartKey();
                break;
        }
    }

    private void HandleRestartKey()
    {
        if (State != GameState.Dead && State != GameState.Won)
            return;

        if (_gameOverPending)
        {
            _gameOverPending = false;
            GoToTitle();
        }
        else
        {
            HideMessage();
            State = GameState.Playing;
            PlayerRespawned?.Invoke();
        }
    }

    private void StartGame()
    {
        HideTitleMenu();
        EnsureHud();
        Score = 0;
        Lives = _startingLives;
        _gameOverPending = false;
        UpdateHudCoins();
        UpdateHudLives();
        HideMessage();
        State = GameState.Playing;
        GameStarted?.Invoke();
    }

    private void Pause()
    {
        if (State != GameState.Playing)
            return;

        State = GameState.Paused;
        ShowPauseMenu();
    }

    private void Resume()
    {
        if (State != GameState.Paused)
            return;

        HidePauseMenu();
        State = GameState.Playing;
    }

    private void RestartLevel()
    {
        HidePauseMenu();
        Score = 0;
        Lives = _startingLives;
        _gameOverPending = false;
        UpdateHudCoins();
        UpdateHudLives();
        HideMessage();
        State = GameState.Playing;
        LevelRestarted?.Invoke();
    }

    private void GoToTitle()
    {
        HidePauseMenu();
        HideMessage();
        Score = 0;
        Lives = _startingLives;
        _gameOverPending = false;
        State = GameState.Title;
        ShowTitleMenu();
        ReturnedToTitle?.Invoke();
    }

    private bool WasClicked(string tag) =>
        _world.TryGetEntity(tag, out var entity)
        && _world.TryGetComponent<UiButtonState>(entity, out var state)
        && state.WasClicked;

    private void ShowTitleMenu()
    {
        ClearOverlay();

        var builder = new UiBuilder(_world, _windowSize);

        const float buttonWidth = 220f;
        const float buttonHeight = 52f;
        const float gap = 16f;

        var panelWidth = 300f;
        var panelHeight = 220f;
        var panelX = builder.CenterX(panelWidth);
        var panelY = builder.CenterY(panelHeight);

        var btnX = builder.CenterX(buttonWidth);
        var playY = panelY + 70f;
        var quitY = playY + buttonHeight + gap;

        _overlayEntities.Add(
            builder.CreatePanel(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 30, 230))
        );
        _overlayEntities.Add(
            builder.CreateLabel(panelX + 70f, panelY + 18f, "Platformer", 28, Color.White)
        );
        _overlayEntities.Add(
            builder.CreateButton(
                btnX,
                playY,
                buttonWidth,
                buttonHeight,
                new Color(55, 110, 55),
                new Color(80, 155, 80),
                new Color(35, 80, 35),
                tag: "btn-play"
            )
        );
        _overlayEntities.Add(builder.CreateLabel(btnX + 82f, playY + 14f, "Play", 22, Color.White));
        _overlayEntities.Add(
            builder.CreateButton(
                btnX,
                quitY,
                buttonWidth,
                buttonHeight,
                new Color(110, 45, 45),
                new Color(155, 65, 65),
                new Color(80, 30, 30),
                tag: "btn-quit-title"
            )
        );
        _overlayEntities.Add(builder.CreateLabel(btnX + 82f, quitY + 14f, "Quit", 22, Color.White));
    }

    private void HideTitleMenu() => ClearOverlay();

    private void ShowPauseMenu()
    {
        ClearOverlay();

        var builder = new UiBuilder(_world, _windowSize);

        const float buttonWidth = 240f;
        const float buttonHeight = 48f;
        const float gap = 14f;

        var panelWidth = 320f;
        var panelHeight = 260f;
        var panelX = builder.CenterX(panelWidth);
        var panelY = builder.CenterY(panelHeight);

        var btnX = builder.CenterX(buttonWidth);
        var resumeY = panelY + 66f;
        var restartY = resumeY + buttonHeight + gap;
        var quitY = restartY + buttonHeight + gap;

        _overlayEntities.Add(
            builder.CreatePanel(panelX, panelY, panelWidth, panelHeight, new Color(20, 20, 30, 230))
        );
        _overlayEntities.Add(
            builder.CreateLabel(panelX + 95f, panelY + 16f, "Paused", 26, Color.White)
        );

        _overlayEntities.Add(
            builder.CreateButton(
                btnX,
                resumeY,
                buttonWidth,
                buttonHeight,
                new Color(55, 110, 55),
                new Color(80, 155, 80),
                new Color(35, 80, 35),
                tag: "btn-resume"
            )
        );
        _overlayEntities.Add(
            builder.CreateLabel(btnX + 84f, resumeY + 13f, "Resume", 20, Color.White)
        );

        _overlayEntities.Add(
            builder.CreateButton(
                btnX,
                restartY,
                buttonWidth,
                buttonHeight,
                new Color(70, 90, 130),
                new Color(95, 120, 165),
                new Color(50, 65, 95),
                tag: "btn-restart"
            )
        );
        _overlayEntities.Add(
            builder.CreateLabel(btnX + 80f, restartY + 13f, "Restart", 20, Color.White)
        );

        _overlayEntities.Add(
            builder.CreateButton(
                btnX,
                quitY,
                buttonWidth,
                buttonHeight,
                new Color(110, 45, 45),
                new Color(155, 65, 65),
                new Color(80, 30, 30),
                tag: "btn-quit-to-title"
            )
        );
        _overlayEntities.Add(
            builder.CreateLabel(btnX + 48f, quitY + 13f, "Quit to Title", 20, Color.White)
        );
    }

    private void HidePauseMenu() => ClearOverlay();

    private void ClearOverlay()
    {
        foreach (var entity in _overlayEntities)
            _world.DestroyEntity(entity);
        _overlayEntities.Clear();
    }

    private void EnsureHud()
    {
        if (_hudBuilt)
            return;

        _hudBuilt = true;
        RebuildHud();
    }

    private void RebuildHud()
    {
        foreach (var entity in _hudEntities)
            _world.DestroyEntity(entity);
        _hudEntities.Clear();

        var builder = new UiBuilder(_world, _windowSize);

        _hudCoinsLabel = builder.CreateLabel(
            16f,
            12f,
            $"Coins: {Score}",
            20,
            Color.White,
            tag: "hud-coins"
        );
        _hudLivesLabel = builder.CreateLabel(
            16f,
            38f,
            $"Lives: {Lives}",
            20,
            Color.White,
            tag: "hud-lives"
        );
        _hudMessageLabel = builder.CreateLabel(
            builder.CenterX(420f),
            builder.CenterY(0f) - 40f,
            "",
            30,
            new Color(255, 220, 0),
            tag: "hud-message"
        );
        var controlsLabel = builder.CreateLabel(
            16f,
            _windowSize.Y - 28f,
            "A/D move   Space jump   P pause   C debug cam   R restart",
            14,
            new Color(220, 220, 220),
            tag: "hud-controls"
        );

        _hudEntities.AddRange([_hudCoinsLabel, _hudLivesLabel, _hudMessageLabel, controlsLabel]);
    }

    private void UpdateHudCoins() =>
        _world.AddComponent(
            _hudCoinsLabel,
            new UiLabel
            {
                Text = $"Coins: {Score}",
                FontSize = 20,
                Color = Color.White,
            }
        );

    private void UpdateHudLives() =>
        _world.AddComponent(
            _hudLivesLabel,
            new UiLabel
            {
                Text = $"Lives: {Lives}",
                FontSize = 20,
                Color = Color.White,
            }
        );

    private void ShowMessage(string text) =>
        _world.AddComponent(
            _hudMessageLabel,
            new UiLabel
            {
                Text = text,
                FontSize = 30,
                Color = new Color(255, 220, 0),
            }
        );

    private void HideMessage() => ShowMessage("");
}
