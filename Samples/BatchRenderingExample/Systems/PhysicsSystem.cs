using BatchRenderingExample.Components;

using Yaeger.ECS;
using Yaeger.Graphics;

namespace BatchRenderingExample.Systems;

public class PhysicsSystem(World world)
{
    public void Update(float deltaTime)
    {
        // Update sprite positions and rotations
        foreach ((Entity entity, Transform2D transform2D, Velocity velocity, RotationSpeed rotationSpeed) in world.Query<Transform2D, Velocity, RotationSpeed>())
        {
            var position = transform2D.Position + velocity.Value * deltaTime;
            var rotation = transform2D.Rotation + rotationSpeed.Value * deltaTime;
            var newVelocity = velocity;

            // Bounce off edges
            if (position.X is < -1f or > 1f)
            {
                newVelocity = new Velocity(velocity.Value with { X = -velocity.Value.X });
                position.X = Math.Clamp(position.X, -1f, 1f);
            }
            if (position.Y is < -1f or > 1f)
            {
                newVelocity = new Velocity(velocity.Value with { Y = -velocity.Value.Y });
                position.Y = Math.Clamp(position.Y, -1f, 1f);
            }

            world.AddComponent(entity, transform2D with { Position = position, Rotation = rotation });
            world.AddComponent(entity, newVelocity);
        }
    }
}