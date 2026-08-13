using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Systems;

/// <summary>
/// Advances every <see cref="Tween"/> component each frame and writes the interpolated value into
/// the corresponding field on its <see cref="Tween.Target"/> entity. Lives in <c>Yaeger.Core</c> —
/// no <c>Window</c>/GL dependency, same as <see cref="TransformHierarchySystem"/>. See docs/tweening.md.
/// </summary>
public class TweenSystem(World world) : IUpdateSystem
{
    /// <inheritdoc/>
    public void Update(float deltaTime)
    {
        // Guard against negative deltaTime, same convention as AnimationSystem.
        if (deltaTime < 0f)
            return;

        foreach ((Entity entity, Tween snapshot) in world.GetStore<Tween>())
        {
            var tween = snapshot;

            // Once a non-looping tween has reached its end, leave the final value in place and
            // stop touching it — Loop/PingPong never set IsFinished, so they always re-evaluate.
            if (tween.IsFinished && tween.LoopMode == TweenLoopMode.Once)
                continue;

            tween.ElapsedTime += deltaTime;
            var activeTime = tween.ElapsedTime - tween.Delay;
            var progress = ComputeProgress(ref tween, activeTime);

            ApplyChannel(tween, Easing.Apply(tween.Easing, progress));

            world.AddComponent(entity, tween);
        }
    }

    private static float ComputeProgress(ref Tween tween, float activeTime)
    {
        // Still waiting out the delay: hold at the start value.
        if (activeTime <= 0f)
            return 0f;

        switch (tween.LoopMode)
        {
            case TweenLoopMode.Once:
                if (activeTime >= tween.Duration)
                {
                    tween.IsFinished = true;
                    return 1f;
                }
                return activeTime / tween.Duration;

            case TweenLoopMode.Loop:
                return activeTime % tween.Duration / tween.Duration;

            case TweenLoopMode.PingPong:
            default:
                var cycle = activeTime % (tween.Duration * 2f);
                return cycle <= tween.Duration
                    ? cycle / tween.Duration
                    : 2f - cycle / tween.Duration;
        }
    }

    private void ApplyChannel(Tween tween, float t)
    {
        switch (tween.Channel)
        {
            case TweenChannel.Transform2DPosition:
                Mutate<Transform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Position = Vector2.Lerp(Vec2(tween.From), Vec2(tween.To), t);
                        return c;
                    }
                );
                break;
            case TweenChannel.Transform2DRotation:
                Mutate<Transform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Rotation = Lerp(tween.From.X, tween.To.X, t);
                        return c;
                    }
                );
                break;
            case TweenChannel.Transform2DScale:
                Mutate<Transform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Scale = Vector2.Lerp(Vec2(tween.From), Vec2(tween.To), t);
                        return c;
                    }
                );
                break;

            case TweenChannel.LocalTransform2DPosition:
                Mutate<LocalTransform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Position = Vector2.Lerp(Vec2(tween.From), Vec2(tween.To), t);
                        return c;
                    }
                );
                break;
            case TweenChannel.LocalTransform2DRotation:
                Mutate<LocalTransform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Rotation = Lerp(tween.From.X, tween.To.X, t);
                        return c;
                    }
                );
                break;
            case TweenChannel.LocalTransform2DScale:
                Mutate<LocalTransform2D>(
                    tween.Target,
                    c =>
                    {
                        c.Scale = Vector2.Lerp(Vec2(tween.From), Vec2(tween.To), t);
                        return c;
                    }
                );
                break;

            case TweenChannel.Transform3DPosition:
                Mutate<Transform3D>(
                    tween.Target,
                    c => c with { Position = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;
            case TweenChannel.Transform3DRotation:
                Mutate<Transform3D>(
                    tween.Target,
                    c => c with { Rotation = Quaternion.Slerp(Quat(tween.From), Quat(tween.To), t) }
                );
                break;
            case TweenChannel.Transform3DScale:
                Mutate<Transform3D>(
                    tween.Target,
                    c => c with { Scale = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;

            case TweenChannel.LocalTransform3DPosition:
                Mutate<LocalTransform3D>(
                    tween.Target,
                    c => c with { Position = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;
            case TweenChannel.LocalTransform3DRotation:
                Mutate<LocalTransform3D>(
                    tween.Target,
                    c => c with { Rotation = Quaternion.Slerp(Quat(tween.From), Quat(tween.To), t) }
                );
                break;
            case TweenChannel.LocalTransform3DScale:
                Mutate<LocalTransform3D>(
                    tween.Target,
                    c => c with { Scale = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;

            case TweenChannel.PointLightIntensity:
                Mutate<PointLight>(
                    tween.Target,
                    c => c with { Intensity = Lerp(tween.From.X, tween.To.X, t) }
                );
                break;
            case TweenChannel.PointLightColor:
                Mutate<PointLight>(
                    tween.Target,
                    c => c with { Color = Color.FromVector4(Vector4.Lerp(tween.From, tween.To, t)) }
                );
                break;

            case TweenChannel.SpotLightIntensity:
                Mutate<SpotLight>(
                    tween.Target,
                    c => c with { Intensity = Lerp(tween.From.X, tween.To.X, t) }
                );
                break;
            case TweenChannel.SpotLightColor:
                Mutate<SpotLight>(
                    tween.Target,
                    c => c with { Color = Color.FromVector4(Vector4.Lerp(tween.From, tween.To, t)) }
                );
                break;

            case TweenChannel.DirectionalLightIntensity:
                Mutate<DirectionalLight>(
                    tween.Target,
                    c => c with { Intensity = Lerp(tween.From.X, tween.To.X, t) }
                );
                break;
            case TweenChannel.DirectionalLightColor:
                Mutate<DirectionalLight>(
                    tween.Target,
                    c => c with { Color = Color.FromVector4(Vector4.Lerp(tween.From, tween.To, t)) }
                );
                break;

            case TweenChannel.Material3DEmissiveColor:
                Mutate<Material3D>(
                    tween.Target,
                    c =>
                        c with
                        {
                            EmissiveColor = Color.FromVector4(
                                Vector4.Lerp(tween.From, tween.To, t)
                            ),
                        }
                );
                break;
            case TweenChannel.Material3DOpacity:
                Mutate<Material3D>(
                    tween.Target,
                    c => c with { Opacity = Lerp(tween.From.X, tween.To.X, t) }
                );
                break;

            case TweenChannel.Camera3DPosition:
                Mutate<Camera3D>(
                    tween.Target,
                    c => c with { Position = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;
            case TweenChannel.Camera3DTarget:
                Mutate<Camera3D>(
                    tween.Target,
                    c => c with { Target = Vector3.Lerp(Vec3(tween.From), Vec3(tween.To), t) }
                );
                break;
        }
    }

    private void Mutate<T>(Entity target, Func<T, T> update)
        where T : struct
    {
        if (world.TryGetComponent<T>(target, out var component))
            world.AddComponent(target, update(component));
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Vector2 Vec2(Vector4 v) => new(v.X, v.Y);

    private static Vector3 Vec3(Vector4 v) => new(v.X, v.Y, v.Z);

    private static Quaternion Quat(Vector4 v) => new(v.X, v.Y, v.Z, v.W);
}
