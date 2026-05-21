using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Systems;

/// <summary>
/// System responsible for rendering text entities in the ECS world.
/// </summary>
public class TextRenderSystem(ITextRenderSurface textRenderer, World world)
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
                text.FontHandle,
                text.FontSize,
                text.Color
            );
        }
    }
}
