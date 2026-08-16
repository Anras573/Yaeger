using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Attach alongside a <see cref="Camera3D"/> to make <see cref="Systems.CameraRigSystem"/> apply
/// trauma-based screen shake: a hit, an explosion, or a landing impact adds <see cref="Trauma"/>,
/// which decays back to zero over time while jittering <see cref="Camera3D.Position"/>/
/// <see cref="Camera3D.Target"/> by an amount proportional to <c>Trauma²</c> — small knocks barely
/// shake, big ones shake hard, matching the standard trauma-shake formulation (Squirrel Eiserloh,
/// GDC 2016) rather than driving amplitude linearly from trauma.
/// </summary>
/// <remarks>
/// <para>
/// The jitter itself comes from a deterministic pseudo-noise function of <see cref="Seed"/> and
/// elapsed shake time — not <see cref="Random"/> — so the exact same <see cref="Seed"/> and elapsed
/// time always produce the exact same offset. That's what makes shake decay and amplitude testable
/// as pure math (see <see cref="Systems.CameraRigSystem"/>'s internal statics) despite being a
/// randomized-looking effect.
/// </para>
/// <para>
/// <see cref="Elapsed"/> and <see cref="PreviousOffset"/> are internal bookkeeping written by
/// <see cref="Systems.CameraRigSystem"/> each step — don't set them by hand. <c>CameraRigSystem</c>
/// subtracts <see cref="PreviousOffset"/> from the camera before doing anything else each frame and
/// only adds the new offset back at the very end, so shake never permanently accumulates into
/// <see cref="Camera3D.Position"/> even when combined with <see cref="Systems.FreeFlyCameraSystem"/>
/// or other code that moves the camera by a relative delta — as long as those other systems run
/// <em>before</em> <c>CameraRigSystem</c> each frame (see docs/camera.md).
/// </para>
/// </remarks>
public struct CameraShake
{
    /// <summary>
    /// Current shake energy, clamped to [0, 1]. Add to this (e.g. via <see cref="AddTrauma"/>) to
    /// trigger or intensify a shake; it decays towards zero on its own at <see cref="Decay"/> per
    /// second.
    /// </summary>
    public float Trauma;

    /// <summary>Trauma removed per second. Must be non-negative.</summary>
    public float Decay;

    /// <summary>
    /// World-space jitter bound per axis at <see cref="Trauma"/> = 1 — each of X/Y/Z is offset by
    /// at most this much independently, so the combined offset vector's length can be up to
    /// <c>√3×</c> this value. Must be non-negative.
    /// </summary>
    public float MaxOffset;

    /// <summary>Seed for the deterministic per-axis noise function — different seeds shake differently.</summary>
    public int Seed;

    /// <summary>
    /// Seconds of shake time elapsed, advanced by <see cref="Systems.CameraRigSystem"/> every step
    /// regardless of <see cref="Trauma"/> so the noise phase keeps moving smoothly across
    /// separate shake triggers instead of restarting at zero each time. Internal bookkeeping.
    /// </summary>
    public float Elapsed;

    /// <summary>
    /// The offset <see cref="Systems.CameraRigSystem"/> added to the camera last step, so it can
    /// be exactly subtracted back out before computing this step's offset. Internal bookkeeping.
    /// </summary>
    public Vector3 PreviousOffset;

    /// <param name="decay">Trauma removed per second. Must be non-negative. Defaults to 1.5.</param>
    /// <param name="maxOffset">Per-axis world-space jitter bound at Trauma = 1. Must be non-negative. Defaults to 0.3.</param>
    /// <param name="seed">Noise seed. Defaults to 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="decay"/> or <paramref name="maxOffset"/> is negative or not finite.
    /// </exception>
    public CameraShake(float decay = 1.5f, float maxOffset = 0.3f, int seed = 0)
    {
        if (!float.IsFinite(decay) || decay < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(decay),
                decay,
                "Decay must be a non-negative, finite value."
            );
        if (!float.IsFinite(maxOffset) || maxOffset < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(maxOffset),
                maxOffset,
                "MaxOffset must be a non-negative, finite value."
            );

        Trauma = 0f;
        Decay = decay;
        MaxOffset = maxOffset;
        Seed = seed;
        Elapsed = 0f;
        PreviousOffset = Vector3.Zero;
    }

    /// <summary>
    /// Returns a copy with <see cref="Trauma"/> increased by <paramref name="amount"/>, clamped to
    /// [0, 1] — the usual way to trigger a shake on an entity that already carries a
    /// <see cref="CameraShake"/> (a fresh hit adds on top of whatever's still decaying, rather than
    /// resetting it).
    /// </summary>
    public readonly CameraShake AddTrauma(float amount) =>
        this with
        {
            Trauma = Math.Clamp(Trauma + amount, 0f, 1f),
        };
}
