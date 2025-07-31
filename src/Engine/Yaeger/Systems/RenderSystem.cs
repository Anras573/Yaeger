using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Systems;

public class RenderSystem(Renderer renderer, World world)
{
    public void Render()
    {
        renderer.BeginFrame();

        foreach ((Entity _, Sprite sprite, Transform2D transform) in world.Query<Sprite, Transform2D>())
        {
            var texture = sprite.TexturePath;
            var transformMatrix = transform.TransformMatrix;

            renderer.DrawQuad(transformMatrix, texture);
        }
        
        renderer.EndFrame();
    }
}

