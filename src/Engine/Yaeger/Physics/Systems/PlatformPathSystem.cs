using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Drives every <see cref="PlatformPath"/> entity's <see cref="Velocity2D"/> towards its current
/// waypoint, advancing (ping-pong or loop) whenever it arrives. Purely a velocity-setter — actual
/// movement still happens through <c>MovementSystem</c>/<c>PhysicsWorld2D.Update</c>, same as any
/// other kinematic body.
/// </summary>
public class PlatformPathSystem(World world) : IUpdateSystem
{
    // How close to a waypoint counts as "arrived". Larger than a typical float-precision
    // residual, small enough not to visibly cut a corner at ordinary platform speeds.
    private const float ArrivalEpsilon = 0.01f;

    public void Update(float deltaTime)
    {
        // Query enumerates the PlatformPath store; we only write back Velocity2D and this same
        // store, so no snapshot needed.
        foreach (
            (Entity entity, PlatformPath pathSnapshot, Transform2D transform) in world.Query<
                PlatformPath,
                Transform2D
            >()
        )
        {
            var path = pathSnapshot;
            var position = transform.Position;

            var toTarget = path.Waypoints[path.CurrentWaypointIndex] - position;
            if (toTarget.LengthSquared() <= ArrivalEpsilon * ArrivalEpsilon)
            {
                Advance(ref path);
                toTarget = path.Waypoints[path.CurrentWaypointIndex] - position;
            }

            var distance = toTarget.Length();
            var newVelocity =
                distance > 1e-6f
                    ? new Velocity2D(toTarget / distance * path.Speed)
                    : Velocity2D.Zero;

            world.AddComponent(entity, newVelocity);
            world.AddComponent(entity, path);
        }
    }

    /// <summary>
    /// Moves <see cref="PlatformPath.CurrentWaypointIndex"/> to the next target: for a ping-pong
    /// path, reversing direction upon reaching either end; for a looping path, wrapping back to
    /// the first waypoint after the last.
    /// </summary>
    private static void Advance(ref PlatformPath path)
    {
        if (!path.PingPong)
        {
            path.CurrentWaypointIndex = (path.CurrentWaypointIndex + 1) % path.Waypoints.Length;
            return;
        }

        if (path.MovingForward)
        {
            if (path.CurrentWaypointIndex >= path.Waypoints.Length - 1)
            {
                path.MovingForward = false;
                path.CurrentWaypointIndex--;
            }
            else
            {
                path.CurrentWaypointIndex++;
            }
        }
        else
        {
            if (path.CurrentWaypointIndex <= 0)
            {
                path.MovingForward = true;
                path.CurrentWaypointIndex++;
            }
            else
            {
                path.CurrentWaypointIndex--;
            }
        }
    }
}
