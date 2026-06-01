using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Updates the <see cref="Transform2D"/> position of every entity that carries a
/// <see cref="ParallaxLayer"/> component so that the layer appears to scroll at the
/// fraction of the camera speed defined by the layer's scroll factors.
///
/// Call <see cref="Update"/> once per frame before rendering, after any camera movement.
/// </summary>
public class ParallaxSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        var cameraPos = Vector2.Zero;
        foreach ((Entity _, Camera2D camera) in world.GetStore<Camera2D>().All())
        {
            cameraPos = camera.Position;
            break;
        }

        foreach (
            (Entity entity, ParallaxLayer layer, Transform2D transform) in world.Query<
                ParallaxLayer,
                Transform2D
            >()
        )
        {
            var newTransform = transform;
            newTransform.Position = new Vector2(
                layer.BasePosition.X + cameraPos.X * (1f - layer.ScrollFactorX),
                layer.BasePosition.Y + cameraPos.Y * (1f - layer.ScrollFactorY)
            );
            world.AddComponent(entity, newTransform);
        }
    }
}
