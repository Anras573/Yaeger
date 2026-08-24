using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Drives an entity's <see cref="Transform3D"/> along a polyline of waypoints at constant world
/// speed via <see cref="Systems.PathFollow3DSystem"/>, turning to face the direction of travel
/// instead of snapping. The 3D counterpart of <see cref="Physics.Components.PlatformPath"/> —
/// waypoints, speed, and a loop mode — with the addition of gradual facing, since a walking or
/// flying 3D entity (unlike a kinematic 2D platform) usually needs to visibly turn.
/// </summary>
/// <remarks>
/// <para>
/// Pair with a <see cref="Transform3D"/> (whatever position/rotation it starts at). Unlike
/// <see cref="Physics.Components.PlatformPath"/>, this component does not need a
/// <c>RigidBody2D</c>/<c>Velocity2D</c> counterpart — <see cref="Systems.PathFollow3DSystem"/>
/// writes <see cref="Transform3D"/> directly, since 3D physics does not exist yet (see #199 for
/// 3D ray queries, the closest thing so far). Games that want the path-follower to interact with
/// obstacles must implement that themselves — see the issue's "out of scope" section.
/// </para>
/// <para>
/// <see cref="LoopMode"/> reuses <see cref="TweenLoopMode"/>'s vocabulary: <c>Once</c> holds at
/// the last waypoint once reached, <c>Loop</c> heads straight from the last waypoint back to the
/// first and repeats, and <c>PingPong</c> reverses direction at either end.
/// </para>
/// <para>
/// <see cref="CurrentSpeed"/> is written by the system every step: it is <see cref="Speed"/>
/// while actually advancing along the path, or zero while holding (a finished <c>Once</c> path,
/// or a degenerate path that cannot move — see the system's remarks). Read it to pick a
/// locomotion animation clip (idle vs. walk/run) without recomputing displacement yourself.
/// </para>
/// </remarks>
public struct PathFollow3D
{
    /// <summary>
    /// The points the entity travels between, in world units. Fewer than two waypoints is a
    /// degenerate path: the system leaves the entity in place (<see cref="CurrentSpeed"/> stays
    /// zero) rather than throwing.
    /// </summary>
    public Vector3[] Waypoints;

    /// <summary>Travel speed in units per second. Must be positive.</summary>
    public float Speed;

    /// <summary>
    /// Maximum turn rate in radians per second used to slerp <see cref="Transform3D.Rotation"/>
    /// towards the direction of travel. Zero never turns (facing stays as authored); a very large
    /// value effectively snaps. Must be non-negative.
    /// </summary>
    public float TurnRate;

    /// <summary>Behaviour once the last waypoint is reached. Defaults to <see cref="TweenLoopMode.Loop"/>.</summary>
    public TweenLoopMode LoopMode;

    /// <summary>
    /// When <c>true</c> (the default), facing only rotates around <see cref="Up"/> — the usual
    /// choice for a walking character on flat-ish ground. When <c>false</c>, facing tracks the
    /// full 3D direction of travel, including pitch — for a bird, drone, or camera rig following
    /// a path through the air.
    /// </summary>
    public bool YawOnly;

    /// <summary>
    /// The up axis used both to build the facing rotation and, when <see cref="YawOnly"/> is
    /// <c>true</c>, to define the plane travel direction is projected onto before facing it.
    /// Defaults to <see cref="Vector3.UnitY"/>.
    /// </summary>
    public Vector3 Up;

    /// <summary>
    /// Index into <see cref="Waypoints"/> of the point currently being travelled towards. Written
    /// by the system — treat as read-only from game code.
    /// </summary>
    public int CurrentWaypointIndex;

    /// <summary>
    /// For <see cref="TweenLoopMode.PingPong"/> paths, whether <see cref="CurrentWaypointIndex"/>
    /// is currently advancing (<c>true</c>) or retreating (<c>false</c>). Written by the system —
    /// treat as read-only from game code. Unused for the other loop modes.
    /// </summary>
    public bool MovingForward;

    /// <summary>
    /// Set once <see cref="LoopMode"/> is <see cref="TweenLoopMode.Once"/> and the last waypoint
    /// has been reached; mirrors <see cref="Tween.IsFinished"/>-style completion flags elsewhere
    /// in the engine. Always <c>false</c> for <see cref="TweenLoopMode.Loop"/>
    /// and <see cref="TweenLoopMode.PingPong"/>, which never finish. Written by the system —
    /// treat as read-only from game code.
    /// </summary>
    public bool IsFinished;

    /// <summary>
    /// The entity's current speed along the path: <see cref="Speed"/> while actively advancing,
    /// or zero while holding. Written by the system every step — see the type's remarks.
    /// </summary>
    public float CurrentSpeed;

    /// <summary>
    /// Creates a path-follow behavior starting at <see cref="Waypoints"/>[0] (if any) and
    /// initially heading towards <see cref="Waypoints"/>[1] (if there is one).
    /// </summary>
    /// <param name="waypoints">
    /// The points to travel between, in world units. Fewer than two is accepted (see the field's
    /// remarks) rather than rejected, so a path can be authored incrementally.
    /// </param>
    /// <param name="speed">Travel speed in units per second. Must be positive.</param>
    /// <param name="turnRate">
    /// Maximum turn rate in radians per second. Must be non-negative. Defaults to <c>2π</c> (one
    /// full turn per second).
    /// </param>
    /// <param name="loopMode">Behaviour once the last waypoint is reached. Defaults to <see cref="TweenLoopMode.Loop"/>.</param>
    /// <param name="yawOnly">Whether facing tracks yaw only (default) or the full 3D direction of travel.</param>
    /// <param name="up">
    /// The up axis for facing. Defaults to <see cref="Vector3.UnitY"/> when <c>null</c> or zero.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="speed"/> is not a positive finite value, or
    /// <paramref name="turnRate"/> is not a non-negative finite value.
    /// </exception>
    public PathFollow3D(
        Vector3[] waypoints,
        float speed,
        float turnRate = MathF.Tau,
        TweenLoopMode loopMode = TweenLoopMode.Loop,
        bool yawOnly = true,
        Vector3? up = null
    )
    {
        if (speed <= 0 || !float.IsFinite(speed))
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Speed must be a positive finite value."
            );
        if (turnRate < 0 || !float.IsFinite(turnRate))
            throw new ArgumentOutOfRangeException(
                nameof(turnRate),
                turnRate,
                "Turn rate must be a non-negative finite value."
            );

        Waypoints = waypoints ?? [];
        Speed = speed;
        TurnRate = turnRate;
        LoopMode = loopMode;
        YawOnly = yawOnly;
        Up =
            up is null || up.Value.LengthSquared() == 0f
                ? Vector3.UnitY
                : Vector3.Normalize(up.Value);

        CurrentWaypointIndex = Waypoints.Length > 1 ? 1 : 0;
        MovingForward = true;
        IsFinished = false;
        CurrentSpeed = 0f;
    }
}
