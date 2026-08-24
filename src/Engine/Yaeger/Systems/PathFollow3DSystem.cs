using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances every <see cref="PathFollow3D"/> entity's <see cref="Transform3D"/> along its
/// waypoint polyline at constant world speed, and slerps <see cref="Transform3D.Rotation"/>
/// towards the direction of travel at the component's configured turn rate. The 3D counterpart
/// of <c>Physics.Systems.PlatformPathSystem</c> — pure component math, no <c>Window</c>/GL
/// dependency, so it compiles into <c>Yaeger.Core</c> and needs no live OpenGL context to test.
/// </summary>
/// <remarks>
/// <para>
/// <b>Constant speed across leg boundaries.</b> A single <see cref="Update"/> call can carry an
/// entity past one or more waypoints without stalling or overshooting: the step's movement budget
/// (<c>Speed * deltaTime</c>) is consumed leg by leg — whatever is left over after reaching a
/// waypoint keeps being spent on the next leg in the same call, so a large <c>deltaTime</c> (or a
/// short leg) never leaves speed "on the table" for one frame.
/// </para>
/// <para>
/// <b>Loop modes</b> reuse <see cref="TweenLoopMode"/>: <see cref="TweenLoopMode.Once"/> holds at
/// the last waypoint once reached (<see cref="PathFollow3D.IsFinished"/> is set, and the entity is
/// skipped on later steps); <see cref="TweenLoopMode.Loop"/> heads straight from the last waypoint
/// back to the first and repeats; <see cref="TweenLoopMode.PingPong"/> reverses at either end.
/// </para>
/// <para>
/// <b>Facing.</b> While actually advancing, rotation is slerped towards the direction of the leg
/// currently being travelled, at up to <see cref="PathFollow3D.TurnRate"/> radians per second —
/// never snapping. <see cref="PathFollow3D.YawOnly"/> projects that direction onto the plane
/// perpendicular to <see cref="PathFollow3D.Up"/> first (the usual choice for a walking character);
/// when the direction of travel is exactly parallel to <c>Up</c> (straight up/down) yaw is
/// undefined, so facing simply holds rather than producing NaN. A path with no <c>TurnRate</c>
/// (zero) never turns; rotation is left exactly as authored.
/// </para>
/// <para>
/// <b>Degenerate paths.</b> Fewer than two waypoints, or a run of duplicate/coincident waypoints
/// that leaves nothing to travel, are handled without throwing or producing NaN: the entity's
/// position and rotation are simply left as-is and <see cref="PathFollow3D.CurrentSpeed"/> reports
/// zero. A bounded number of zero-length-leg advances per step (rather than an unbounded loop)
/// guards against a pathological all-coincident waypoint cycle spinning forever.
/// </para>
/// </remarks>
public class PathFollow3DSystem(World world) : IUpdateSystem
{
    // How close to a waypoint counts as "arrived" / a zero-length leg. Larger than a typical
    // float-precision residual, small enough not to visibly cut a corner at ordinary speeds.
    private const float ArrivalEpsilon = 1e-4f;

    /// <inheritdoc/>
    public void Update(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            return;

        // Query enumerates the PathFollow3D store; we only write back Transform3D and this same
        // store, so no snapshot needed.
        foreach (
            (Entity entity, PathFollow3D pathSnapshot, Transform3D transform) in world.Query<
                PathFollow3D,
                Transform3D
            >()
        )
        {
            var path = pathSnapshot;
            var waypoints = path.Waypoints;

            if (waypoints is null || waypoints.Length < 2 || path.IsFinished)
            {
                path.CurrentSpeed = 0f;
                world.AddComponent(entity, path);
                continue;
            }

            var position = transform.Position;
            var remaining = path.Speed * deltaTime;
            var travelDirection = Vector3.Zero;
            var moved = false;

            // Bounds the number of zero-length-leg advances a single step can make, so an all-
            // coincident waypoint cycle can't spin forever without ever consuming `remaining`.
            var maxAdvances = waypoints.Length * 2 + 4;

            for (var i = 0; i < maxAdvances && remaining > ArrivalEpsilon; i++)
            {
                var target = waypoints[path.CurrentWaypointIndex];
                var toTarget = target - position;
                var distance = toTarget.Length();

                if (distance <= ArrivalEpsilon)
                {
                    if (!Advance(ref path))
                        break;
                    continue;
                }

                var direction = toTarget / distance;

                if (remaining >= distance)
                {
                    position = target;
                    remaining -= distance;
                    moved = true;
                    travelDirection = direction;

                    if (!Advance(ref path))
                        break;
                }
                else
                {
                    position += direction * remaining;
                    moved = true;
                    travelDirection = direction;
                    remaining = 0f;
                }
            }

            path.CurrentSpeed = moved && !path.IsFinished ? path.Speed : 0f;

            var rotation = transform.Rotation;
            if (moved && path.TurnRate > 0f)
            {
                var facing = ComputeFacing(travelDirection, path.Up, path.YawOnly);
                if (facing is { } targetRotation)
                    rotation = RotateTowards(rotation, targetRotation, path.TurnRate * deltaTime);
            }

            world.AddComponent(entity, new Transform3D(position, rotation, transform.Scale));
            world.AddComponent(entity, path);
        }
    }

    /// <summary>
    /// Moves <see cref="PathFollow3D.CurrentWaypointIndex"/> to the next target, per
    /// <see cref="PathFollow3D.LoopMode"/>. Returns <c>false</c> when an <see cref="TweenLoopMode.Once"/>
    /// path has reached its last waypoint (setting <see cref="PathFollow3D.IsFinished"/>) — the
    /// caller must stop consuming its movement budget in that case, there being nowhere left to go.
    /// </summary>
    private static bool Advance(ref PathFollow3D path)
    {
        switch (path.LoopMode)
        {
            case TweenLoopMode.Loop:
                path.CurrentWaypointIndex = (path.CurrentWaypointIndex + 1) % path.Waypoints.Length;
                return true;

            case TweenLoopMode.PingPong:
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
                return true;

            case TweenLoopMode.Once:
            default:
                if (path.CurrentWaypointIndex >= path.Waypoints.Length - 1)
                {
                    path.IsFinished = true;
                    return false;
                }
                path.CurrentWaypointIndex++;
                return true;
        }
    }

    /// <summary>
    /// The rotation that faces <paramref name="direction"/>, or <c>null</c> when facing is
    /// undefined for this step (a zero/non-finite direction, or — when <paramref name="yawOnly"/>
    /// is <c>true</c> — a direction exactly parallel to <paramref name="up"/>, which has no yaw
    /// component to extract). A <c>null</c> result means the caller should leave rotation as-is.
    /// </summary>
    internal static Quaternion? ComputeFacing(Vector3 direction, Vector3 up, bool yawOnly)
    {
        if (direction == Vector3.Zero || !IsFinite(direction))
            return null;

        direction = Vector3.Normalize(direction);
        up = up == Vector3.Zero || !IsFinite(up) ? Vector3.UnitY : Vector3.Normalize(up);

        if (yawOnly)
        {
            var projected = direction - up * Vector3.Dot(direction, up);
            if (projected.LengthSquared() < 1e-8f)
                return null;

            direction = Vector3.Normalize(projected);
        }

        return LookRotation(direction, up);
    }

    /// <summary>
    /// Builds the rotation whose local <c>-Z</c> maps to <paramref name="forward"/> (the same
    /// look-down-negative-Z convention used elsewhere in the engine, e.g. <c>AudioSpatialMath</c>
    /// and glTF cameras), with local <c>+Y</c> as close to <paramref name="up"/> as an orthonormal
    /// basis allows.
    /// </summary>
    internal static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        forward = Vector3.Normalize(forward);

        var right = Vector3.Cross(forward, up);
        if (right.LengthSquared() < 1e-8f)
        {
            // forward is parallel to up: pick an arbitrary reference to derive a right vector from.
            var reference = MathF.Abs(forward.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
            right = Vector3.Cross(forward, reference);
        }
        right = Vector3.Normalize(right);
        var orthogonalUp = Vector3.Cross(right, forward);

        var basis = new Matrix4x4(
            right.X,
            right.Y,
            right.Z,
            0f,
            orthogonalUp.X,
            orthogonalUp.Y,
            orthogonalUp.Z,
            0f,
            -forward.X,
            -forward.Y,
            -forward.Z,
            0f,
            0f,
            0f,
            0f,
            1f
        );

        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(basis));
    }

    /// <summary>
    /// Slerps <paramref name="current"/> towards <paramref name="target"/> by at most
    /// <paramref name="maxRadiansDelta"/> radians (taking the shorter arc), rather than an
    /// exponential-smoothing fraction — this is what gives <see cref="PathFollow3D.TurnRate"/> a
    /// constant angular speed instead of the fast-then-slow feel exponential smoothing has.
    /// </summary>
    internal static Quaternion RotateTowards(
        Quaternion current,
        Quaternion target,
        float maxRadiansDelta
    )
    {
        if (maxRadiansDelta <= 0f)
            return current;

        var dot = Quaternion.Dot(current, target);
        if (dot < 0f)
        {
            target = new Quaternion(-target.X, -target.Y, -target.Z, -target.W);
            dot = -dot;
        }

        dot = Math.Clamp(dot, -1f, 1f);
        var angle = 2f * MathF.Acos(dot);

        if (angle <= maxRadiansDelta || angle < 1e-6f)
            return Quaternion.Normalize(target);

        return Quaternion.Normalize(Quaternion.Slerp(current, target, maxRadiansDelta / angle));
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
