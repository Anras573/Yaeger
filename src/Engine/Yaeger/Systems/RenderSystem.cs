using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Systems;

public class RenderSystem(Renderer renderer, World world)
{
    public void Render()
    {
        renderer.BeginFrame();

        // Render plain sprites (full texture UV).
        foreach (
            (Entity _, Sprite sprite, Transform2D transform) in world.Query<Sprite, Transform2D>()
        )
        {
            renderer.DrawQuad(transform.TransformMatrix, sprite.TexturePath);
        }

        // Render sprite-sheet entities: UV sub-region is derived from the current animation frame.
        foreach (
            (
                Entity _,
                SpriteSheet sheet,
                AnimationState state,
                Transform2D transform
            ) in world.Query<SpriteSheet, AnimationState, Transform2D>()
        )
        {
            var frameIndex = Math.Clamp(state.CurrentFrameIndex, 0, sheet.FrameCount - 1);
            var (uvMin, uvMax) = sheet.GetFrameUv(frameIndex);
            renderer.DrawQuad(transform.TransformMatrix, sheet.TexturePath, uvMin, uvMax);
        }

        renderer.EndFrame();
    }
}
