using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances skeletal animation each frame. For every entity carrying a <see cref="SkeletonHandle"/>
/// and an <see cref="AnimationPlayer"/>, it advances playback time, samples the current clip into
/// per-bone local transforms, resolves the skinning matrix palette via the
/// <see cref="Skeleton"/> hierarchy, and writes it to a <see cref="BonePalette"/> component for the
/// mesh render system to upload. Entities with no current clip resolve to the bind pose.
/// Wire to <see cref="Yaeger.Windowing.Window.OnUpdate"/>.
/// </summary>
/// <remarks>
/// Call <see cref="CrossFadeTo"/> to blend into a new clip over a short duration instead of
/// snapping to it. While a fade is in progress, <see cref="Update"/> samples both the incoming and
/// outgoing clips into per-bone locals and interpolates between them (lerp translation/scale, slerp
/// rotation) before resolving the palette; once the fade duration elapses it drops back to the
/// single-clip path. The outgoing clip keeps advancing its own playback time during the fade, so a
/// looping source clip doesn't freeze on the pose it was at when the fade began.
/// </remarks>
public sealed class SkeletalAnimationSystem(World world, SkeletonRegistry skeletons) : IUpdateSystem
{
    // Scratch buffers, grown as needed and reused across entities and frames so sampling allocates
    // nothing. The TRS buffers are only touched while a crossfade is in progress.
    private Matrix4x4[] _localTransforms = [];
    private Vector3[] _toTranslations = [];
    private Quaternion[] _toRotations = [];
    private Vector3[] _toScales = [];
    private Vector3[] _fromTranslations = [];
    private Quaternion[] _fromRotations = [];
    private Vector3[] _fromScales = [];

    public void Update(float deltaTime)
    {
        // Guard the per-frame delta: a negative or non-finite frame time would rewind or poison every
        // player at once. (Intentional reverse playback is a per-player concern via a negative
        // AnimationPlayer.Speed, applied below.)
        if (!float.IsFinite(deltaTime) || deltaTime < 0f)
            deltaTime = 0f;

        // Force iteration over the SkeletonHandle store (index 0): the loop writes back the
        // AnimationPlayer (and BonePalette) component, so enumerating the AnimationPlayer store
        // would mutate the dictionary mid-iteration. Without forcing, Query auto-picks the smaller
        // store, which could be AnimationPlayer.
        foreach (
            (Entity entity, SkeletonHandle handle, AnimationPlayer player) in world.Query<
                SkeletonHandle,
                AnimationPlayer
            >(0)
        )
        {
            if (!skeletons.TryGet(handle, out var skeleton))
                continue;

            var boneCount = skeleton.Bones.Length;
            if (boneCount == 0)
                continue;

            AnimationClip? toClip = null;
            if (!string.IsNullOrEmpty(player.CurrentClip))
                skeletons.TryGetClip(handle, player.CurrentClip, out toClip);

            var updated = player;
            // Keep every time/duration field finite so the modulo/clamp logic and sampling stay
            // well-defined, and so the stored component never holds NaN/Infinity for other systems
            // to trip over.
            var sanitized = false;
            sanitized |= Sanitize(ref updated.Time);
            sanitized |= Sanitize(ref updated.Speed);
            sanitized |= Sanitize(ref updated.PreviousTime);
            sanitized |= Sanitize(ref updated.FadeDuration, min: 0f);
            sanitized |= Sanitize(ref updated.FadeElapsed, min: 0f);

            // wasFading reflects the player's state coming into this frame; fading advances (and
            // can complete) below.
            var wasFading = updated.FadeDuration > 0f;
            if (wasFading)
                updated.FadeElapsed += deltaTime;

            var fadeComplete = wasFading && updated.FadeElapsed >= updated.FadeDuration;
            var isFading = wasFading && !fadeComplete;

            if (fadeComplete)
            {
                // The fade finishes this frame: drop back to the single-clip path immediately
                // rather than blending an imperceptible sliver of the outgoing pose for one more
                // frame.
                updated.PreviousClip = null;
                updated.PreviousTime = 0f;
                updated.FadeDuration = 0f;
                updated.FadeElapsed = 0f;
            }

            var toAdvanced = AdvanceTime(
                ref updated.Time,
                toClip?.Duration ?? 0f,
                updated.Loop,
                updated.Speed,
                deltaTime
            );

            AnimationClip? fromClip = null;
            if (isFading && !string.IsNullOrEmpty(updated.PreviousClip))
                skeletons.TryGetClip(handle, updated.PreviousClip, out fromClip);

            var fromAdvanced =
                isFading
                && AdvanceTime(
                    ref updated.PreviousTime,
                    fromClip?.Duration ?? 0f,
                    updated.Loop,
                    updated.Speed,
                    deltaTime
                );

            if (sanitized || toAdvanced || fromAdvanced || wasFading)
                world.AddComponent(entity, updated);

            if (_localTransforms.Length < boneCount)
                _localTransforms = new Matrix4x4[boneCount];
            var locals = _localTransforms.AsSpan(0, boneCount);

            if (isFading)
            {
                var alpha = updated.FadeElapsed / updated.FadeDuration;
                SampleBlended(
                    skeleton,
                    toClip,
                    updated.Time,
                    fromClip,
                    updated.PreviousTime,
                    alpha,
                    locals
                );
            }
            else
            {
                SampleSingle(skeleton, toClip, updated.Time, locals);
            }

            // Reuse the entity's palette array when it is already large enough.
            if (
                !world.TryGetComponent<BonePalette>(entity, out var palette)
                || palette.Matrices is null
                || palette.Matrices.Length < boneCount
            )
            {
                palette = new BonePalette(new Matrix4x4[boneCount]);
            }

            skeleton.ComputeMatrixPalette(locals, palette.Matrices.AsSpan(0, boneCount));
            world.AddComponent(entity, palette);
        }
    }

    /// <summary>
    /// Begins a crossfade from <paramref name="entity"/>'s current clip (and playback time) into
    /// <paramref name="clipName"/> over <paramref name="duration"/> seconds. <see cref="Update"/>
    /// blends the two poses until the fade elapses, then drops back to the single-clip path.
    /// </summary>
    /// <param name="entity">The entity to retarget. Must carry an <see cref="AnimationPlayer"/>.</param>
    /// <param name="clipName">The clip to fade into, or <c>null</c> to fade to the bind pose.</param>
    /// <param name="duration">
    /// Fade length in seconds. A non-positive or non-finite value snaps to
    /// <paramref name="clipName"/> immediately — the same hard switch as assigning
    /// <see cref="AnimationPlayer.CurrentClip"/> directly — and clears any fade already in
    /// progress.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="entity"/> has no <see cref="AnimationPlayer"/>.
    /// </exception>
    public void CrossFadeTo(Entity entity, string? clipName, float duration)
    {
        if (!world.TryGetComponent<AnimationPlayer>(entity, out var player))
            throw new InvalidOperationException(
                "Entity does not have an AnimationPlayer component."
            );

        if (!float.IsFinite(duration) || duration <= 0f)
        {
            player.CurrentClip = clipName;
            player.Time = 0f;
            player.PreviousClip = null;
            player.PreviousTime = 0f;
            player.FadeDuration = 0f;
            player.FadeElapsed = 0f;
            world.AddComponent(entity, player);
            return;
        }

        player.PreviousClip = player.CurrentClip;
        player.PreviousTime = player.Time;
        player.CurrentClip = clipName;
        player.Time = 0f;
        player.FadeDuration = duration;
        player.FadeElapsed = 0f;
        world.AddComponent(entity, player);
    }

    private static bool Sanitize(ref float value, float min = float.NegativeInfinity)
    {
        if (float.IsFinite(value) && value >= min)
            return false;
        value = 0f;
        return true;
    }

    /// <summary>
    /// Advances <paramref name="time"/> by <paramref name="deltaTime"/> * <paramref name="speed"/>
    /// and wraps/clamps it against <paramref name="duration"/>, mirroring the single-clip path's
    /// existing playback rules. Returns whether the caller's component needs to be persisted.
    /// </summary>
    private static bool AdvanceTime(
        ref float time,
        float duration,
        bool loop,
        float speed,
        float deltaTime
    )
    {
        if (duration <= 0f)
            return false;

        time += deltaTime * speed;

        if (loop)
        {
            time %= duration;
            if (time < 0f)
                time += duration;
        }
        else
        {
            time = Math.Clamp(time, 0f, duration);
        }

        return true;
    }

    private void SampleSingle(
        Skeleton skeleton,
        AnimationClip? clip,
        float time,
        Span<Matrix4x4> locals
    )
    {
        // Seed locals with the bind pose so bones without a track keep their rest transform.
        for (var i = 0; i < locals.Length; i++)
            locals[i] = skeleton.Bones[i].LocalTransform;

        clip?.Sample(time, locals);
    }

    private void SampleBlended(
        Skeleton skeleton,
        AnimationClip? toClip,
        float toTime,
        AnimationClip? fromClip,
        float fromTime,
        float alpha,
        Span<Matrix4x4> locals
    )
    {
        var boneCount = locals.Length;
        EnsureCapacity(ref _toTranslations, boneCount);
        EnsureCapacity(ref _toRotations, boneCount);
        EnsureCapacity(ref _toScales, boneCount);
        EnsureCapacity(ref _fromTranslations, boneCount);
        EnsureCapacity(ref _fromRotations, boneCount);
        EnsureCapacity(ref _fromScales, boneCount);

        var toT = _toTranslations.AsSpan(0, boneCount);
        var toR = _toRotations.AsSpan(0, boneCount);
        var toS = _toScales.AsSpan(0, boneCount);
        var fromT = _fromTranslations.AsSpan(0, boneCount);
        var fromR = _fromRotations.AsSpan(0, boneCount);
        var fromS = _fromScales.AsSpan(0, boneCount);

        // Seed both poses with the (decomposed) bind pose, so a bone without a track in either clip
        // falls back to its rest pose exactly like the single-clip path does.
        for (var i = 0; i < boneCount; i++)
        {
            var (translation, rotation, scale) = DecomposeBindPose(
                skeleton.Bones[i].LocalTransform
            );
            toT[i] = translation;
            toR[i] = rotation;
            toS[i] = scale;
            fromT[i] = translation;
            fromR[i] = rotation;
            fromS[i] = scale;
        }

        toClip?.SampleTRS(toTime, toT, toR, toS);
        fromClip?.SampleTRS(fromTime, fromT, fromR, fromS);

        var clampedAlpha = Math.Clamp(alpha, 0f, 1f);
        for (var i = 0; i < boneCount; i++)
        {
            var translation = Vector3.Lerp(fromT[i], toT[i], clampedAlpha);
            var rotation = Quaternion.Slerp(fromR[i], toR[i], clampedAlpha);
            var scale = Vector3.Lerp(fromS[i], toS[i], clampedAlpha);
            locals[i] =
                Matrix4x4.CreateScale(scale)
                * Matrix4x4.CreateFromQuaternion(rotation)
                * Matrix4x4.CreateTranslation(translation);
        }
    }

    private static (Vector3 Translation, Quaternion Rotation, Vector3 Scale) DecomposeBindPose(
        Matrix4x4 bindPose
    ) =>
        Matrix4x4.Decompose(bindPose, out var scale, out var rotation, out var translation)
            ? (translation, rotation, scale)
            : (bindPose.Translation, Quaternion.Identity, Vector3.One);

    private static void EnsureCapacity<T>(ref T[] array, int length)
    {
        if (array.Length < length)
            array = new T[length];
    }
}
