using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Input;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.UI;
using Yaeger.Windowing;

// UiDemo: shows a main menu with Play / Quit buttons. Clicking Play switches to a HUD
// that counts elapsed frames. Clicking Quit (or pressing ESC) exits.
//
// Controls:
//   Play button   — enter the game view
//   Quit button   — exit
//   ESC           — exit

using var window = Window.Create();
var world = new World();
var fontManager = new FontManager();

var font = fontManager.Load("Assets/Roboto-Regular.ttf");
var textRenderer = new TextRenderer(window, fontManager);
var uiRenderer = new UiRenderer(window);
var uiSystem = new UiSystem(world);
var uiRenderSystem = new UiRenderSystem(world, uiRenderer, textRenderer, font, window);

var frameCount = 0;
var menuEntities = new List<Entity>();
Entity hudScoreLabel = default;
var inGame = false;

window.OnLoad += () =>
{
    var windowSize = window.Size;
    BuildMenu(windowSize);
};

window.OnUpdate += _ =>
{
    uiSystem.Update(0f);

    if (!inGame)
    {
        if (world.TryGetEntity("btn-play", out var btnPlay)
            && world.TryGetComponent<UiButtonState>(btnPlay, out var playState)
            && playState.WasClicked)
        {
            EnterGame();
        }
    }

    if (world.TryGetEntity("btn-quit", out var btnQuit)
        && world.TryGetComponent<UiButtonState>(btnQuit, out var quitState)
        && quitState.WasClicked)
    {
        window.Close();
    }

    if (inGame)
    {
        frameCount++;
        world.AddComponent(hudScoreLabel, new UiLabel
        {
            Text = $"Score: {frameCount}",
            FontSize = 20,
            Color = Color.White,
        });
    }
};

window.OnRender += _ =>
{
    uiRenderer.Clear(Color.Black);
    uiRenderSystem.Render();
};

window.OnClosing += () =>
{
    uiRenderer.Dispose();
    textRenderer.Dispose();
    fontManager.Dispose();
};

Keyboard.AddKeyDown(Keys.Escape, window.Close);

window.Run();

return;

void BuildMenu(Vector2 windowSize)
{
    var builder = new UiBuilder(world, windowSize);

    const float buttonWidth = 200f;
    const float buttonHeight = 50f;
    const float gap = 16f;

    var panelWidth = 280f;
    var panelHeight = 200f;
    var panelX = builder.CenterX(panelWidth);
    var panelY = builder.CenterY(panelHeight);

    var btnX = builder.CenterX(buttonWidth);
    var playY = panelY + 60f;
    var quitY = playY + buttonHeight + gap;

    var panel = builder.CreatePanel(
        panelX, panelY, panelWidth, panelHeight,
        new Color(30, 30, 40, 220)
    );

    var title = builder.CreateLabel(
        panelX + 65f, panelY + 16f,
        "Main Menu", 26, Color.White
    );

    var btnPlay = builder.CreateButton(
        btnX, playY, buttonWidth, buttonHeight,
        new Color(55, 110, 55), new Color(80, 155, 80), new Color(35, 80, 35),
        tag: "btn-play"
    );

    var lblPlay = builder.CreateLabel(
        btnX + 72f, playY + 13f,
        "Play", 22, Color.White
    );

    var btnQuit = builder.CreateButton(
        btnX, quitY, buttonWidth, buttonHeight,
        new Color(110, 45, 45), new Color(155, 65, 65), new Color(80, 30, 30),
        tag: "btn-quit"
    );

    var lblQuit = builder.CreateLabel(
        btnX + 72f, quitY + 13f,
        "Quit", 22, Color.White
    );

    menuEntities.AddRange(new[] { panel, title, btnPlay, lblPlay, btnQuit, lblQuit });
}

void EnterGame()
{
    inGame = true;

    foreach (var e in menuEntities)
        world.DestroyEntity(e);
    menuEntities.Clear();

    var windowSize = window.Size;
    var builder = new UiBuilder(world, windowSize);

    builder.CreatePanel(10, 10, 180, 40, new Color(0, 0, 0, 160));

    hudScoreLabel = builder.CreateLabel(18, 18, "Score: 0", 20, Color.White);

    builder.CreateButton(
        windowSize.X - 120f, 10f, 110f, 36f,
        new Color(110, 45, 45), new Color(155, 65, 65), new Color(80, 30, 30),
        tag: "btn-quit"
    );

    builder.CreateLabel(windowSize.X - 80f, 20f, "Quit", 20, Color.White);
}
