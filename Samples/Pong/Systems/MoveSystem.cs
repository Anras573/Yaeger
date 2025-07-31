using Pong.Components;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Pong.Systems;

public class MoveSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        // Move all entities with Velocity and Transform2D
        foreach ((Entity entity, Transform2D transform, Velocity velocity, Bounds bounds) in world.Query<Transform2D, Velocity, Bounds>())
        {
            var position = transform.Position;
            position += velocity.Value * deltaTime;
            var scale = transform.Scale;
            var half = scale.Y / 2f;
            
            if (bounds.ClampY)
                position.Y = Math.Clamp(position.Y, bounds.MinY + half, bounds.MaxY - half);
            
            world.AddComponent(entity, transform with { Position = position });
        }
    }
}