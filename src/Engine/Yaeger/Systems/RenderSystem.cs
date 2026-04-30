using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;
using Yaeger.Windowing;

namespace Yaeger.Systems;

public class RenderSystem(Renderer renderer, World world, Window? window = null)
{
    public void Render()
    {
        UpdateCamera();

        renderer.BeginFrame();
        var spriteSheetStore = world.GetStore<SpriteSheet>();
        var animationStateStore = world.GetStore<AnimationState>();

        // Submit plain sprites (full texture UV).
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
            renderer.SubmitQuad(transform.TransformMatrix, sprite.TexturePath);
        }

        // Submit sprite-sheet entities: UV sub-region is derived from the current animation frame.
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
            renderer.SubmitQuad(transform.TransformMatrix, sheet.TexturePath, uvMin, uvMax);
        }

        renderer.EndFrame();
    }

    private void UpdateCamera()
    {
        // Without a Window we can't derive aspect ratio; leave the renderer's view-projection alone.
        if (window is null)
        {
            return;
        }

        foreach (var (_, camera) in world.GetStore<Camera2D>().All())
        {
            var size = window.Size;
            var aspectRatio = size.Y > 0 ? size.X / size.Y : 1f;
            renderer.SetCamera(camera.ViewProjection(aspectRatio));
            return;
        }

        renderer.SetCamera(Matrix4x4.Identity);
    }
}
