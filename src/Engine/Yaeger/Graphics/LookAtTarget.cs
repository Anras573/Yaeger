using Yaeger.ECS;

namespace Yaeger.Graphics;

/// <summary>
/// Attach alongside a <see cref="Camera3D"/> to make <see cref="Systems.CameraRigSystem"/> track
/// <see cref="TargetEntity"/>'s <see cref="Transform3D"/> with <see cref="Camera3D.Target"/> every
/// step: the same framerate-independent exponential smoothing <see cref="CameraFollow"/> uses on
/// the 2D side.
/// </summary>
/// <remarks>
/// If <see cref="TargetEntity"/> is destroyed or otherwise loses its <see cref="Transform3D"/>,
/// <see cref="Camera3D.Target"/> simply holds its last value rather than snapping or throwing —
/// the same graceful-degradation contract <see cref="Systems.CameraFollowSystem"/> has for a
/// missing <see cref="Transform2D"/>.
/// </remarks>
public struct LookAtTarget
{
    /// <summary>The entity whose <see cref="Transform3D"/> position the camera looks at.</summary>
    public Entity TargetEntity;

    /// <summary>
    /// Exponential smoothing rate, in 1/seconds — higher values catch up to the target faster.
    /// A value of zero or less snaps directly to the target every step (no interpolation).
    /// Defaults to 5.
    /// </summary>
    public float Smoothing;

    /// <summary>Creates a look-at behavior targeting <paramref name="targetEntity"/>.</summary>
    /// <param name="targetEntity">The entity to look at.</param>
    /// <param name="smoothing">
    /// Exponential smoothing rate in 1/seconds. Non-positive snaps directly. Defaults to 5.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="smoothing"/> is not finite.
    /// </exception>
    public LookAtTarget(Entity targetEntity, float smoothing = 5f)
    {
        if (!float.IsFinite(smoothing))
            throw new ArgumentOutOfRangeException(
                nameof(smoothing),
                smoothing,
                "Smoothing must be a finite value."
            );

        TargetEntity = targetEntity;
        Smoothing = smoothing;
    }
}
