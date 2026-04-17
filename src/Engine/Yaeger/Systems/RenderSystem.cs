using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Systems;

public class RenderSystem(Renderer renderer, World world)
{
    public void Render()
    {
        renderer.BeginFrame();
        var spriteSheetStore = world.GetStore<SpriteSheet>();
        var animationStateStore = world.GetStore<AnimationState>();

        // Render plain sprites (full texture UV).
        foreach (
            (Entity entity, Sprite sprite, Transform2D transform) in world.Query<
                Sprite,
                Transform2D
            >()
        )
        {
            if (spriteSheetStore.TryGet(entity, out _) && animationStateStore.TryGet(entity, out _))
            {
                continue;
            }
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
            if (sheet.FrameCount <= 0)
            {
                continue;
            }
            var frameIndex = Math.Clamp(state.CurrentFrameIndex, 0, sheet.FrameCount - 1);
            var (uvMin, uvMax) = sheet.GetFrameUv(frameIndex);
            renderer.DrawQuad(transform.TransformMatrix, sheet.TexturePath, uvMin, uvMax);
        }

        renderer.EndFrame();
    }
}
