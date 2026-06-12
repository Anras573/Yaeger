using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;
using Yaeger.Rendering;
using Yaeger.UI;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Renders UI: draws panel and button backgrounds via <see cref="UiRenderer"/>,
/// then delegates labels to <see cref="ITextRenderSurface"/>. All rendering is
/// screen-space and does not honour the <c>Camera2D</c>.
///
/// Call <see cref="Render"/> from your window's render callback, after the main
/// game render systems.
/// </summary>
public class UiRenderSystem : IRenderSystem
{
    private readonly World _world;
    private readonly UiRenderer _uiRenderer;
    private readonly ITextRenderSurface _textRenderer;
    private readonly IFontHandle _defaultFont;
    private readonly Window _window;

    /// <param name="world">The ECS world containing UI entities.</param>
    /// <param name="uiRenderer">Colored-quad renderer for panels and buttons.</param>
    /// <param name="textRenderer">Text renderer for labels.</param>
    /// <param name="defaultFont">Font used by all <see cref="UiLabel"/> components.</param>
    /// <param name="window">Used to read the current window size each frame.</param>
    public UiRenderSystem(
        World world,
        UiRenderer uiRenderer,
        ITextRenderSurface textRenderer,
        IFontHandle defaultFont,
        Window window
    )
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(uiRenderer);
        ArgumentNullException.ThrowIfNull(textRenderer);
        ArgumentNullException.ThrowIfNull(defaultFont);
        ArgumentNullException.ThrowIfNull(window);

        _world = world;
        _uiRenderer = uiRenderer;
        _textRenderer = textRenderer;
        _defaultFont = defaultFont;
        _window = window;
    }

    public void Render()
    {
        var windowSize = _window.Size;
        _uiRenderer.BeginFrame(windowSize);

        RenderPanels();
        RenderButtons();

        _uiRenderer.EndFrame();

        RenderLabels(windowSize);
    }

    private void RenderPanels()
    {
        foreach ((Entity _, UiRect rect, UiPanel panel) in _world.Query<UiRect, UiPanel>())
        {
            _uiRenderer.SubmitRect(rect.Position, rect.Size, panel.BackgroundColor);
        }
    }

    private void RenderButtons()
    {
        var buttonStateStore = _world.GetStore<UiButtonState>();

        foreach ((Entity entity, UiRect rect, UiButton button) in _world.Query<UiRect, UiButton>())
        {
            var color = button.Normal;
            if (buttonStateStore.TryGet(entity, out var state))
            {
                color =
                    state.IsPressed ? button.Pressed
                    : state.IsHovered ? button.Hovered
                    : button.Normal;
            }
            _uiRenderer.SubmitRect(rect.Position, rect.Size, color);
        }
    }

    private void RenderLabels(Vector2 windowSize)
    {
        foreach ((Entity _, UiRect rect, UiLabel label) in _world.Query<UiRect, UiLabel>())
        {
            if (string.IsNullOrEmpty(label.Text))
                continue;

            var fontSize = (int)MathF.Round(MathF.Max(1f, label.FontSize));

            // Convert UiRect.Position to NDC. TextRenderer places glyphs relative to this
            // point as the baseline origin — text ascenders extend upward from rect.Position.Y.
            var ndcX = windowSize.X > 0 ? (rect.Position.X / windowSize.X) * 2f - 1f : 0f;
            var ndcY = windowSize.Y > 0 ? 1f - (rect.Position.Y / windowSize.Y) * 2f : 0f;

            // Independent X/Y scales convert glyph pixel units to NDC and preserve aspect ratio.
            var scaleX = windowSize.X > 0 ? 2f / windowSize.X : 0f;
            var scaleY = windowSize.Y > 0 ? 2f / windowSize.Y : 0f;

            var transform =
                Matrix4x4.CreateScale(scaleX, scaleY, 1f)
                * Matrix4x4.CreateTranslation(ndcX, ndcY, 0f);

            _textRenderer.DrawText(label.Text, transform, _defaultFont, fontSize, label.Color);
        }
    }
}
