using System.Numerics;

namespace Yaeger.Physics.Components;

/// <summary>
/// Optional helper component that drives a kinematic platform's <see cref="Velocity2D"/> back
/// and forth (or around a loop) through a list of waypoints, via
/// <see cref="Systems.PlatformPathSystem"/>. Games are free to drive a platform's
/// <see cref="Velocity2D"/> themselves instead — this is purely a convenience for the common
/// "moves between two or more points" case, not a required part of the moving-platform/rider
/// pipeline (see <see cref="Systems.CharacterControllerSystem"/>'s rider-carrying remarks).
/// </summary>
/// <remarks>
/// Pair with a <see cref="Transform2D"/> (whatever position it starts at) and a
/// <see cref="Velocity2D"/> (written by the system) on an entity with a kinematic
/// <see cref="RigidBody2D"/>, so <c>MovementSystem</c> actually integrates the velocity this
/// system computes into position.
/// </remarks>
public struct PlatformPath
{
    /// <summary>
    /// The points the platform travels between, in world units. Must contain at least two.
    /// </summary>
    public Vector2[] Waypoints;

    /// <summary>
    /// Travel speed in units per second. Must be positive.
    /// </summary>
    public float Speed;

    /// <summary>
    /// When <c>true</c>, the platform reverses direction at either end of the waypoint list
    /// (ping-pong). When <c>false</c>, it loops: after reaching the last waypoint it heads
    /// straight back to the first and repeats.
    /// </summary>
    public bool PingPong;

    /// <summary>
    /// Index into <see cref="Waypoints"/> of the point currently being travelled towards.
    /// Written by the system — treat as read-only from game code.
    /// </summary>
    public int CurrentWaypointIndex;

    /// <summary>
    /// For <see cref="PingPong"/> paths, whether <see cref="CurrentWaypointIndex"/> is currently
    /// advancing (<c>true</c>) or retreating (<c>false</c>) through the waypoint list. Written
    /// by the system — treat as read-only from game code. Unused when <see cref="PingPong"/> is
    /// <c>false</c>.
    /// </summary>
    public bool MovingForward;

    /// <summary>
    /// Creates a platform path starting at <see cref="Waypoints"/>[0] and initially heading
    /// towards <see cref="Waypoints"/>[1].
    /// </summary>
    /// <param name="waypoints">At least two points to travel between.</param>
    /// <param name="speed">Travel speed in units per second. Must be positive.</param>
    /// <param name="pingPong">
    /// Whether to reverse at either end (<c>true</c>) or loop back to the start (<c>false</c>,
    /// the default).
    /// </param>
    /// <exception cref="ArgumentException">Thrown when fewer than two waypoints are given.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="speed"/> is not a positive finite value.
    /// </exception>
    public PlatformPath(Vector2[] waypoints, float speed, bool pingPong = false)
    {
        if (waypoints.Length < 2)
            throw new ArgumentException("At least two waypoints are required.", nameof(waypoints));
        if (speed <= 0 || !float.IsFinite(speed))
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Speed must be a positive finite value."
            );

        Waypoints = waypoints;
        Speed = speed;
        PingPong = pingPong;
        CurrentWaypointIndex = 1;
        MovingForward = true;
    }
}
