using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Systems;

/// <summary>
/// System responsible for rendering text entities in the ECS world.
/// </summary>
public class TextRenderSystem(TextRenderer textRenderer, World world)
{
    /// <summary>
    /// Renders all entities that have both Text and Transform2D components.
    /// </summary>
    public void Render()
    {
        foreach ((Entity _, Text text, Transform2D transform) in world.Query<Text, Transform2D>())
        {
            textRenderer.DrawText(
                text.Content,
                transform.TransformMatrix,
                text.Font,
                text.FontSize,
                text.Color
            );
        }
    }
}