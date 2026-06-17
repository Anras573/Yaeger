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
public sealed class SkeletalAnimationSystem(World world, SkeletonRegistry skeletons) : IUpdateSystem
{
    // Scratch buffer for per-bone local transforms, grown as needed and reused across entities and
    // frames so sampling allocates nothing.
    private Matrix4x4[] _localTransforms = [];

    public void Update(float deltaTime)
    {
        // Never step backwards; a negative delta would rewind playback unexpectedly.
        if (deltaTime < 0f)
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

            AnimationClip? clip = null;
            if (!string.IsNullOrEmpty(player.CurrentClip))
                skeletons.TryGetClip(handle, player.CurrentClip, out clip);

            var updated = player;
            if (clip is { Duration: > 0f })
            {
                var speed = float.IsFinite(player.Speed) ? player.Speed : 0f;
                updated.Time += deltaTime * speed;

                if (updated.Loop)
                {
                    updated.Time %= clip.Duration;
                    if (updated.Time < 0f)
                        updated.Time += clip.Duration;
                }
                else
                {
                    updated.Time = Math.Clamp(updated.Time, 0f, clip.Duration);
                }

                world.AddComponent(entity, updated);
            }

            // Seed locals with the bind pose so bones without a track keep their rest transform.
            if (_localTransforms.Length < boneCount)
                _localTransforms = new Matrix4x4[boneCount];
            for (var i = 0; i < boneCount; i++)
                _localTransforms[i] = skeleton.Bones[i].LocalTransform;

            var locals = _localTransforms.AsSpan(0, boneCount);
            clip?.Sample(updated.Time, locals);

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
}
