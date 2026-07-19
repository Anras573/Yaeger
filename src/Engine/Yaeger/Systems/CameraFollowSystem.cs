using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Windowing;

namespace Yaeger.Systems;

/// <summary>
/// Moves every <see cref="CameraFollow"/> entity's <see cref="Camera2D.Position"/> towards its
/// target each step: exponential smoothing, an optional deadzone, optional velocity-based
/// look-ahead, and — when a <see cref="CameraBounds"/> is also present on the entity — clamping
/// so the camera's visible span never shows past the bounds.
/// </summary>
/// <remarks>
/// Run this system after gameplay/physics update it, so it reads final, post-movement target
/// positions. If <see cref="CameraFollow.TargetEntity"/> no longer has a <see cref="Transform2D"/>
/// (destroyed or otherwise despawned), the camera simply holds its last position for that
/// entity this step, rather than snapping or throwing.
/// </remarks>
public class CameraFollowSystem(World world, Window? window = null) : IUpdateSystem
{
    public void Update(float deltaTime)
    {
        var aspectRatio = ComputeAspectRatio(window);

        // Query enumerates the CameraFollow store; we only write back Camera2D, so no snapshot
        // needed — iterating a different store than we're mutating.
        foreach (
            (Entity entity, CameraFollow follow, Camera2D camera) in world.Query<
                CameraFollow,
                Camera2D
            >()
        )
        {
            if (!world.TryGetComponent<Transform2D>(follow.TargetEntity, out var targetTransform))
                continue;

            var desiredPosition = targetTransform.Position;
            if (
                follow.LookAheadTime > 0f
                && world.TryGetComponent<Velocity2D>(follow.TargetEntity, out var targetVelocity)
            )
                desiredPosition += targetVelocity.Linear * follow.LookAheadTime;

            var deadzoneTarget = ApplyDeadzone(
                camera.Position,
                desiredPosition,
                follow.DeadzoneHalfExtents
            );

            var newCamera = camera;
            newCamera.Position = ApplySmoothing(
                camera.Position,
                deadzoneTarget,
                follow.Smoothing,
                deltaTime
            );

            if (world.TryGetComponent<CameraBounds>(entity, out var bounds))
                newCamera.Position = ClampToBounds(
                    newCamera.Position,
                    newCamera.Zoom,
                    aspectRatio,
                    bounds
                );

            world.AddComponent(entity, newCamera);
        }
    }

    /// <summary>
    /// Returns <paramref name="desiredPosition"/> unchanged if it falls within
    /// <paramref name="deadzoneHalfExtents"/> of <paramref name="currentPosition"/>; otherwise
    /// returns the position pulled back to the deadzone's edge (the minimum correction needed to
    /// keep the target inside the box).
    /// </summary>
    internal static Vector2 ApplyDeadzone(
        Vector2 currentPosition,
        Vector2 desiredPosition,
        Vector2 deadzoneHalfExtents
    )
    {
        var delta = desiredPosition - currentPosition;
        var overflowX =
            MathF.Max(0f, MathF.Abs(delta.X) - deadzoneHalfExtents.X) * MathF.Sign(delta.X);
        var overflowY =
            MathF.Max(0f, MathF.Abs(delta.Y) - deadzoneHalfExtents.Y) * MathF.Sign(delta.Y);
        return currentPosition + new Vector2(overflowX, overflowY);
    }

    /// <summary>
    /// Framerate-independent exponential smoothing towards <paramref name="targetPosition"/>.
    /// <paramref name="smoothing"/> zero or less snaps directly (no interpolation).
    /// </summary>
    internal static Vector2 ApplySmoothing(
        Vector2 currentPosition,
        Vector2 targetPosition,
        float smoothing,
        float deltaTime
    )
    {
        if (smoothing <= 0f)
            return targetPosition;

        var factor = 1f - MathF.Exp(-smoothing * deltaTime);
        return Vector2.Lerp(currentPosition, targetPosition, factor);
    }

    /// <summary>
    /// Clamps <paramref name="position"/> so the camera's visible span at
    /// <paramref name="zoom"/>/<paramref name="aspectRatio"/> (half-extents
    /// (aspectRatio / zoom, 1 / zoom), per <see cref="Camera2D"/>'s remarks) stays within
    /// <paramref name="bounds"/>. An axis where the bounds are narrower than the viewport is
    /// centered on the bounds' own midpoint instead.
    /// </summary>
    internal static Vector2 ClampToBounds(
        Vector2 position,
        float zoom,
        float aspectRatio,
        CameraBounds bounds
    )
    {
        var z = zoom > 0f ? zoom : 1f;
        var halfExtents = new Vector2(aspectRatio / z, 1f / z);

        return new Vector2(
            ClampAxis(position.X, bounds.Min.X, bounds.Max.X, halfExtents.X),
            ClampAxis(position.Y, bounds.Min.Y, bounds.Max.Y, halfExtents.Y)
        );
    }

    private static float ClampAxis(float value, float min, float max, float halfExtent)
    {
        var minCenter = min + halfExtent;
        var maxCenter = max - halfExtent;
        return minCenter <= maxCenter
            ? Math.Clamp(value, minCenter, maxCenter)
            : (minCenter + maxCenter) / 2f;
    }

    private static float ComputeAspectRatio(Window? window)
    {
        if (window is null)
            return 1f;

        var size = window.Size;
        return size.Y > 0 ? size.X / size.Y : 1f;
    }
}
