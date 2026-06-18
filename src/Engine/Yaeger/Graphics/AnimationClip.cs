using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>A keyframe for a translation or scale channel: a <see cref="Vector3"/> at a time (seconds).</summary>
public readonly record struct VectorKey(float Time, Vector3 Value);

/// <summary>A keyframe for a rotation channel: a <see cref="Quaternion"/> at a time (seconds).</summary>
public readonly record struct QuaternionKey(float Time, Quaternion Value);

/// <summary>
/// Keyframe tracks for a single bone. Each channel is sampled independently and recomposed into a
/// local transform via <see cref="Sample"/>. Empty channels fall back to a neutral value
/// (zero translation, identity rotation, unit scale).
/// </summary>
public record BoneTrack(
    int BoneIndex,
    VectorKey[] Positions,
    QuaternionKey[] Rotations,
    VectorKey[] Scales
)
{
    /// <summary>Samples the track at <paramref name="time"/> (seconds) into a local transform matrix.</summary>
    public Matrix4x4 Sample(float time)
    {
        var translation = SampleVector(Positions, time, Vector3.Zero);
        var rotation = SampleQuaternion(Rotations, time);
        var scale = SampleVector(Scales, time, Vector3.One);

        return Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(translation);
    }

    private static Vector3 SampleVector(VectorKey[] keys, float time, Vector3 fallback)
    {
        if (keys.Length == 0)
            return fallback;
        if (keys.Length == 1 || time <= keys[0].Time)
            return keys[0].Value;
        if (time >= keys[^1].Time)
            return keys[^1].Value;

        // Inlined segment search (no Func<> closure) to keep per-frame sampling allocation-free.
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var t1 = keys[i + 1].Time;
            if (time < t1)
            {
                var t0 = keys[i].Time;
                var span = t1 - t0;
                var t = span > 0f ? Math.Clamp((time - t0) / span, 0f, 1f) : 0f;
                return Vector3.Lerp(keys[i].Value, keys[i + 1].Value, t);
            }
        }
        return keys[^1].Value;
    }

    private static Quaternion SampleQuaternion(QuaternionKey[] keys, float time)
    {
        if (keys.Length == 0)
            return Quaternion.Identity;
        if (keys.Length == 1 || time <= keys[0].Time)
            return keys[0].Value;
        if (time >= keys[^1].Time)
            return keys[^1].Value;

        // Inlined segment search (no Func<> closure) to keep per-frame sampling allocation-free.
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var t1 = keys[i + 1].Time;
            if (time < t1)
            {
                var t0 = keys[i].Time;
                var span = t1 - t0;
                var t = span > 0f ? Math.Clamp((time - t0) / span, 0f, 1f) : 0f;
                return Quaternion.Slerp(keys[i].Value, keys[i + 1].Value, t);
            }
        }
        return keys[^1].Value;
    }
}

/// <summary>
/// A named animation clip: a set of per-bone keyframe tracks evaluated over a fixed duration
/// (seconds). Sampling produces local transforms that a <see cref="Skeleton"/> resolves into a
/// skinning palette.
/// </summary>
public record AnimationClip(string Name, float Duration, BoneTrack[] Tracks)
{
    /// <summary>
    /// Samples every track at <paramref name="time"/> (seconds) and writes each track's local
    /// transform into <paramref name="localTransforms"/> at the track's bone index. Bones without a
    /// track are left untouched, so callers should pre-seed the span with bind-pose locals.
    /// </summary>
    public void Sample(float time, Span<Matrix4x4> localTransforms)
    {
        foreach (var track in Tracks)
        {
            if (track.BoneIndex >= 0 && track.BoneIndex < localTransforms.Length)
                localTransforms[track.BoneIndex] = track.Sample(time);
        }
    }
}
