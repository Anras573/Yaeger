using System.Numerics;
using Yaeger.ECS;

namespace Yaeger.Graphics;

/// <summary>
/// Attach alongside a <see cref="Camera2D"/> to make <see cref="Systems.CameraFollowSystem"/>
/// track <see cref="TargetEntity"/>'s <see cref="Transform2D"/> every step: framerate-independent
/// exponential smoothing, an optional deadzone the target can move within before the camera
/// reacts, and optional look-ahead in the target's current <see cref="Physics.Components.Velocity2D"/>
/// direction.
/// </summary>
/// <remarks>
/// Pair with a <see cref="CameraBounds"/> on the same entity to additionally
/// clamp the camera so its visible span never shows past a level's edges. If
/// <see cref="TargetEntity"/> is destroyed or otherwise loses its <see cref="Transform2D"/>, the
/// camera simply holds its last position rather than snapping or erroring.
/// </remarks>
public struct CameraFollow
{
    /// <summary>The entity whose <see cref="Transform2D"/> the camera follows.</summary>
    public Entity TargetEntity;

    /// <summary>
    /// Exponential smoothing rate, in 1/seconds — higher values catch up to the target faster.
    /// A value of zero or less snaps directly to the target every step (no interpolation).
    /// Defaults to 5.
    /// </summary>
    public float Smoothing;

    /// <summary>
    /// Half-width/half-height, in world units, of a box centered on the camera's current
    /// position within which the target can move freely without the camera reacting. Once the
    /// target moves outside this box, the camera is pulled just far enough to keep the target at
    /// the box's edge. Zero (the default) disables the deadzone — the camera always tracks the
    /// target directly (subject to <see cref="Smoothing"/>).
    /// </summary>
    public Vector2 DeadzoneHalfExtents;

    /// <summary>
    /// Seconds of look-ahead: the camera targets <c>TargetEntity</c>'s position plus its current
    /// <see cref="Physics.Components.Velocity2D"/> scaled by this value, biasing the view towards
    /// where the target is heading. Has no effect if the target has no <c>Velocity2D</c>
    /// component. Zero (the default) disables look-ahead.
    /// </summary>
    public float LookAheadTime;

    /// <summary>
    /// Creates a camera-follow behavior targeting <paramref name="targetEntity"/>.
    /// </summary>
    /// <param name="targetEntity">The entity to follow.</param>
    /// <param name="smoothing">
    /// Exponential smoothing rate in 1/seconds. Non-positive snaps directly. Defaults to 5.
    /// </param>
    /// <param name="deadzoneHalfExtents">
    /// Half-extents of the no-reaction box, in world units. Both components must be
    /// non-negative. Defaults to zero (no deadzone).
    /// </param>
    /// <param name="lookAheadTime">
    /// Seconds of velocity-based look-ahead. Must be non-negative. Defaults to zero (disabled).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="smoothing"/> is not finite, <paramref name="deadzoneHalfExtents"/>
    /// has a negative or non-finite component, or <paramref name="lookAheadTime"/> is negative or
    /// not finite.
    /// </exception>
    public CameraFollow(
        Entity targetEntity,
        float smoothing = 5f,
        Vector2? deadzoneHalfExtents = null,
        float lookAheadTime = 0f
    )
    {
        if (!float.IsFinite(smoothing))
            throw new ArgumentOutOfRangeException(
                nameof(smoothing),
                smoothing,
                "Smoothing must be a finite value."
            );

        var deadzone = deadzoneHalfExtents ?? Vector2.Zero;
        if (
            deadzone.X < 0
            || deadzone.Y < 0
            || !float.IsFinite(deadzone.X)
            || !float.IsFinite(deadzone.Y)
        )
            throw new ArgumentOutOfRangeException(
                nameof(deadzoneHalfExtents),
                deadzone,
                "Deadzone half-extents must be non-negative, finite values."
            );

        if (lookAheadTime < 0 || !float.IsFinite(lookAheadTime))
            throw new ArgumentOutOfRangeException(
                nameof(lookAheadTime),
                lookAheadTime,
                "Look-ahead time must be a non-negative finite value."
            );

        TargetEntity = targetEntity;
        Smoothing = smoothing;
        DeadzoneHalfExtents = deadzone;
        LookAheadTime = lookAheadTime;
    }
}
