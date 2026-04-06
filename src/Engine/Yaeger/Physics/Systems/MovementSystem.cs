using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Integrates velocity into position and rotation using semi-implicit Euler integration.
/// Also applies linear drag.
/// </summary>
public class MovementSystem(World world) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        // Query enumerates RigidBody2D store; we only write back Transform2D and Velocity2D,
        // so no snapshot needed — iterating a different store than we're mutating.
        foreach (
            (
                Entity entity,
                RigidBody2D body,
                Transform2D transform,
                Velocity2D velocity
            ) in world.Query<RigidBody2D, Transform2D, Velocity2D>()
        )
        {
            if (body.Type == BodyType.Static)
                continue;

            var newVelocity = velocity;

            // Apply linear drag to dynamic bodies
            if (body.Type == BodyType.Dynamic && body.LinearDrag > 0)
            {
                var dragFactor = 1.0f - body.LinearDrag * deltaTime;
                if (dragFactor < 0.0f)
                {
                    dragFactor = 0.0f;
                }
                newVelocity.Linear *= dragFactor;
            }

            // Integrate position and rotation
            var newTransform = transform;
            newTransform.Position += newVelocity.Linear * deltaTime;
            newTransform.Rotation += newVelocity.Angular * deltaTime;

            world.AddComponent(entity, newTransform);
            world.AddComponent(entity, newVelocity);
        }
    }
}
