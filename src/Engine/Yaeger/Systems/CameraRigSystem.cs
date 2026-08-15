using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Drives every <see cref="Camera3D"/> entity's <see cref="Camera3D.Position"/>/
/// <see cref="Camera3D.Target"/>/<see cref="Camera3D.Up"/> from whichever of three optional,
/// independently-usable pieces it carries — <see cref="Transform3D"/>, <see cref="LookAtTarget"/>,
/// <see cref="CameraShake"/> — leaving a camera with none of them exactly as authored (e.g. a
/// plain <see cref="Camera3D"/> driven directly, or by <see cref="FreeFlyCameraSystem"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The <see cref="Transform3D"/> bridge.</b> <see cref="Systems.MeshRenderSystem"/> reads
/// <see cref="Camera3D.Position"/>/<c>Target</c>/<c>Up</c> directly and never consults
/// <see cref="Transform3D"/> — so on its own, a camera entity carrying a <see cref="Parent"/> (and
/// resolved every frame by <see cref="TransformHierarchySystem"/>) would have its world transform
/// silently ignored by the renderer. This system is the fix: when a <see cref="Camera3D"/> entity
/// also carries a <see cref="Transform3D"/>, that transform becomes authoritative every step —
/// <c>Position</c> copies straight across, and <c>Target</c>/<c>Up</c> are derived by rotating a
/// fixed local forward/up (<c>-Z</c>/<c>+Y</c>, the same look-down-negative-Z convention
/// <see cref="Yaeger.Audio.AudioSpatialMath"/> and glTF cameras use) by <see cref="Transform3D.Rotation"/>.
/// Run this system <em>after</em> <see cref="TransformHierarchySystem"/> so a parented camera rides
/// its parent's already-composed world transform, not last frame's.
/// </para>
/// <para>
/// <b>Run order.</b> Like <see cref="Systems.CameraFollowSystem"/>, call <see cref="Update"/> after
/// whatever positions the camera each frame — gameplay code, <see cref="TransformHierarchySystem"/>,
/// <see cref="FreeFlyCameraSystem"/> — and before rendering. This matters most for
/// <see cref="CameraShake"/>: see its own remarks for why shake must be applied last to avoid
/// permanently baking into the camera's position.
/// </para>
/// </remarks>
public sealed class CameraRigSystem(World world) : IUpdateSystem
{
    /// <inheritdoc/>
    public void Update(float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            deltaTime = 0f;

        // Query enumerates the Camera3D store; entities with none of the three optional pieces
        // below are skipped entirely (see the `continue`), so a plain or FreeFly-only camera never
        // pays for — or has its authored/free-fly-driven values touched by — this system.
        foreach ((Entity entity, Camera3D camera) in world.GetStore<Camera3D>())
        {
            var hasShake = world.TryGetComponent<CameraShake>(entity, out var shake);
            var hasTransform = world.TryGetComponent<Transform3D>(entity, out var transform);
            var hasLookAt = world.TryGetComponent<LookAtTarget>(entity, out var lookAt);

            if (!hasShake && !hasTransform && !hasLookAt)
                continue;

            var updated = camera;

            // Undo last step's shake offset first, so every step below reads/writes a clean base —
            // otherwise shake would compound every frame instead of jittering around a fixed rest
            // pose. See CameraShake's remarks.
            if (hasShake)
            {
                updated.Position -= shake.PreviousOffset;
                updated.Target -= shake.PreviousOffset;
            }

            if (hasTransform)
            {
                updated.Position = transform.Position;
                updated.Target = transform.Position + ComputeForward(transform.Rotation);
                updated.Up = ComputeUp(transform.Rotation);
            }

            if (
                hasLookAt
                && world.TryGetComponent<Transform3D>(lookAt.TargetEntity, out var targetTransform)
            )
            {
                // If the target entity has no Transform3D (destroyed or never had one), Target
                // simply holds whatever the steps above already set — graceful degradation,
                // matching CameraFollowSystem's contract for a missing Transform2D.
                updated.Target = ApplySmoothing(
                    updated.Target,
                    targetTransform.Position,
                    lookAt.Smoothing,
                    deltaTime
                );
            }

            if (hasShake)
            {
                shake.Trauma = DecayTrauma(shake.Trauma, shake.Decay, deltaTime);
                // Advance regardless of Trauma so the noise phase keeps moving smoothly across
                // separate triggers instead of restarting at zero each time (see CameraShake).
                shake.Elapsed += deltaTime;

                var offset = ComputeShakeOffset(
                    shake.Seed,
                    shake.Trauma,
                    shake.MaxOffset,
                    shake.Elapsed
                );
                updated.Position += offset;
                updated.Target += offset;
                shake.PreviousOffset = offset;

                world.AddComponent(entity, shake);
            }

            world.AddComponent(entity, updated);
        }
    }

    /// <summary>World-space forward for a <see cref="Transform3D.Rotation"/>: local <c>-Z</c> rotated into world space.</summary>
    internal static Vector3 ComputeForward(Quaternion rotation) =>
        Vector3.Transform(-Vector3.UnitZ, rotation);

    /// <summary>World-space up for a <see cref="Transform3D.Rotation"/>: local <c>+Y</c> rotated into world space.</summary>
    internal static Vector3 ComputeUp(Quaternion rotation) =>
        Vector3.Transform(Vector3.UnitY, rotation);

    /// <summary>
    /// Framerate-independent exponential smoothing towards <paramref name="targetPosition"/>.
    /// <paramref name="smoothing"/> zero or less snaps directly (no interpolation) — the
    /// <see cref="Vector3"/> counterpart of <see cref="CameraFollowSystem.ApplySmoothing"/>.
    /// </summary>
    internal static Vector3 ApplySmoothing(
        Vector3 currentPosition,
        Vector3 targetPosition,
        float smoothing,
        float deltaTime
    )
    {
        if (smoothing <= 0f)
            return targetPosition;

        var factor = 1f - MathF.Exp(-smoothing * deltaTime);
        return Vector3.Lerp(currentPosition, targetPosition, factor);
    }

    /// <summary>Removes <paramref name="decay"/> units of trauma per second, clamped to [0, 1].</summary>
    internal static float DecayTrauma(float trauma, float decay, float deltaTime) =>
        Math.Clamp(trauma - decay * deltaTime, 0f, 1f);

    /// <summary>
    /// The deterministic shake offset for a given <paramref name="seed"/>/<paramref name="trauma"/>/
    /// <paramref name="elapsed"/> shake time: amplitude scales with <c>trauma²</c> (small trauma
    /// barely shakes; see <see cref="CameraShake"/>'s remarks), direction and phase come from
    /// <see cref="Noise"/>.
    /// </summary>
    internal static Vector3 ComputeShakeOffset(
        int seed,
        float trauma,
        float maxOffset,
        float elapsed
    )
    {
        var amplitude = trauma * trauma * maxOffset;
        if (amplitude <= 0f)
            return Vector3.Zero;

        return new Vector3(
                Noise(seed, 0, elapsed),
                Noise(seed, 1, elapsed),
                Noise(seed, 2, elapsed)
            ) * amplitude;
    }

    // Deterministic, smoothly-varying pseudo-noise roughly in [-1, 1]: three sine waves at
    // incommensurate frequencies, phase-offset per seed/axis (via a cheap hash) so different
    // seeds/axes shake differently without needing actual random state — a pure function of
    // (seed, axis, time), which is what makes shake reproducible and unit-testable.
    private static float Noise(int seed, int axis, float time)
    {
        var phase = (seed * 12.9898f + axis * 78.233f) % (2f * MathF.PI);
        return (
                MathF.Sin(time * 13.7f + phase)
                + MathF.Sin(time * 7.3f + phase * 1.7f) * 0.5f
                + MathF.Sin(time * 19.1f + phase * 2.3f) * 0.33f
            ) / 1.83f;
    }
}
