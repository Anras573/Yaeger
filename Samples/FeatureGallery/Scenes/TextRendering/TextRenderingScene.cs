using System.Numerics;
using Yaeger.ECS;
using Yaeger.Font;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Systems;
using Yaeger.Windowing;

namespace FeatureGallery.Scenes.TextRendering;

/// <summary>
/// Text rendering demo: font loading, glyph atlas generation (SDF mode), and batched text
/// rendering across a mix of sizes.
/// </summary>
/// <remarks>
/// The size mix below is deliberate and doubles as a visual regression test:
///   - Multiple sizes of one font coexist — proves the atlas is keyed by (font, size)
///     rather than font alone (fix for a bug where the first size rendered became
///     permanent for the rest of the session).
///   - 22, 18, 14, 10 are NOT multiples of 4 — proves the glyph upload path respects
///     GL_UNPACK_ALIGNMENT = 1 (fix for a bug where sub-multiple-of-4 sizes produced
///     sheared/striped glyphs). If any of these lines render as stripes rather than
///     readable text, the alignment fix has regressed.
/// </remarks>
public sealed class TextRenderingScene : IDemoScene
{
    private const string FontPath = "Assets/Shared/Roboto-Regular.ttf";

    private World? _world;
    private FontManager? _fontManager;
    private TextRenderer? _textRenderer;
    private UnifiedRenderSystem? _renderSystem;
    private Window? _window;
    private bool _screenshotTaken;

    public string Name => "Text Rendering";
    public string Description => "Font loading, glyph atlas, SDF text at mixed sizes";
    public string Controls => "No input — a static rendering showcase";

    public void Load(Window window)
    {
        _window = window;
        _world = new World();
        _fontManager = new FontManager();
        _textRenderer = new TextRenderer(window, _fontManager, TextRenderMode.Sdf);
        _renderSystem = new UnifiedRenderSystem(null, _textRenderer, _world);
        _screenshotTaken = false;

        var font = _fontManager.Load(FontPath);

        AddTextOverlay(font, "Hello, Yaeger!", new Vector2(-0.95f, 0.60f), 32, Color.White);
        AddTextOverlay(
            font,
            "The quick brown fox jumps over the lazy dog",
            new Vector2(-0.95f, 0.35f),
            22,
            Color.Green
        );
        AddTextOverlay(
            font,
            "fontSize 18 renders cleanly (not a multiple of 4)",
            new Vector2(-0.95f, 0.15f),
            18,
            Color.White
        );
        AddTextOverlay(font, "and 14 too", new Vector2(-0.95f, 0.00f), 14, Color.White);
        AddTextOverlay(font, "even 10 pt", new Vector2(-0.95f, -0.12f), 10, Color.White);
        AddTextOverlay(font, "Press ESC for menu", new Vector2(-0.95f, -0.30f), 16, Color.Blue);
    }

    private void AddTextOverlay(
        Font font,
        string content,
        Vector2 position,
        int fontSize,
        Color color
    )
    {
        if (_world is null)
            return;

        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new Text(content, font, fontSize, color));
        _world.AddComponent(
            entity,
            new Transform2D { Position = position, Scale = new Vector2(0.003f, 0.003f) }
        );
    }

    public void Update(float deltaTime) { }

    public void Render(float deltaTime)
    {
        _renderSystem?.Render();

        // Opt-in headless screenshot hook: set YAEGER_SCREENSHOT to a file path to capture the
        // first rendered frame as a PNG — handy for showcasing/reproducing rendering bugs (e.g.
        // from a CI run or a headless Xvfb session) without a human watching the window.
        if (_screenshotTaken || _window is null)
            return;

        var screenshotPath = Environment.GetEnvironmentVariable("YAEGER_SCREENSHOT");
        if (screenshotPath is null)
            return;

        ScreenshotCapture.SaveFramebufferPng(_window, screenshotPath);
        Console.WriteLine($"Screenshot saved to {screenshotPath}");
        _screenshotTaken = true;
        _window.Close();
    }

    public void Dispose()
    {
        _textRenderer?.Dispose();
        _fontManager?.Dispose();
    }
}
