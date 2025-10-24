using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Systems;

/// <summary>
/// System responsible for rendering text entities in the ECS world.
/// </summary>
public class TextRenderSystem
{
    private readonly TextRenderer _textRenderer;
    private readonly World _world;

    public TextRenderSystem(TextRenderer textRenderer, World world)
    {
        _textRenderer = textRenderer ?? throw new ArgumentNullException(nameof(textRenderer));
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    /// <summary>
    /// Renders all entities that have both Text and Transform2D components.
    /// </summary>
    public void Render()
    {
        foreach ((Entity _, Text text, Transform2D transform) in _world.Query<Text, Transform2D>())
        {
            _textRenderer.DrawText(
                text.Content,
                transform.TransformMatrix,
                text.Font,
                text.FontSize,
                text.Color
            );
        }
    }
}