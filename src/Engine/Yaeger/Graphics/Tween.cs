using System.Numerics;
using Yaeger.ECS;

namespace Yaeger.Graphics;

/// <summary>
/// Animates a single <see cref="TweenChannel"/> on <see cref="Target"/> from <see cref="From"/> to
/// <see cref="To"/> over <see cref="Duration"/> seconds, eased by <see cref="Easing"/> and driven
/// each frame by <see cref="Systems.TweenSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>ComponentStorage&lt;T&gt;</c> holds a single component of each type per entity, so an entity
/// carrying its own <see cref="Tween"/> can only animate one channel at a time. <see cref="Target"/>
/// decouples "the entity the Tween component lives on" from "the entity being animated": the common
/// case is self-tweening (<c>Target</c> equal to the entity the component is attached to), but
/// animating two channels on one entity concurrently — a door sliding open while its panel light
/// brightens, say, when both live on the same entity — just needs a second, otherwise-empty carrier
/// entity whose only component is a <see cref="Tween"/> with <see cref="Target"/> pointing at the
/// shared entity. No pooling or fixed track count is needed on either side.
/// </para>
/// <para>
/// <see cref="From"/>/<see cref="To"/> are stored as a <see cref="Vector4"/> regardless of channel:
/// a <c>float</c> channel uses <c>X</c>; <see cref="Vector2"/> uses <c>X</c>/<c>Y</c>;
/// <see cref="Vector3"/> and <see cref="Color"/> use <c>X</c>/<c>Y</c>/<c>Z</c> (<see cref="Color"/>
/// normalised via <see cref="Color.ToVector4"/>); a <see cref="Quaternion"/> channel (3D rotation)
/// uses all four components and is always slerped, never lerped. The <c>Create</c> overloads below
/// pack these conveniently; use the primary constructor directly only when you already have a
/// <see cref="Vector4"/>.
/// </para>
/// </remarks>
public struct Tween
{
    /// <summary>The entity whose component this tween writes to.</summary>
    public Entity Target;

    /// <summary>Which component field on <see cref="Target"/> is animated.</summary>
    public TweenChannel Channel;

    /// <summary>Value at progress 0, packed per <see cref="Channel"/>'s data type (see remarks).</summary>
    public Vector4 From;

    /// <summary>Value at progress 1, packed the same way as <see cref="From"/>.</summary>
    public Vector4 To;

    /// <summary>Seconds from the end of <see cref="Delay"/> to reaching <see cref="To"/>. Must be positive.</summary>
    public float Duration;

    /// <summary>Seconds to wait (holding at <see cref="From"/>) before the tween starts advancing. Non-negative.</summary>
    public float Delay;

    /// <summary>Easing curve applied to normalized progress before interpolating.</summary>
    public EasingFunction Easing;

    /// <summary>Behaviour once progress reaches the end of <see cref="Duration"/>.</summary>
    public TweenLoopMode LoopMode;

    /// <summary>Total time this tween has been alive, including <see cref="Delay"/>. Advanced by <see cref="Systems.TweenSystem"/>.</summary>
    public float ElapsedTime;

    /// <summary>
    /// Set once <see cref="LoopMode"/> is <see cref="TweenLoopMode.Once"/> and progress has reached
    /// <see cref="To"/>; mirrors <see cref="AnimationState.IsFinished"/>. Always <c>false</c> for
    /// <see cref="TweenLoopMode.Loop"/> and <see cref="TweenLoopMode.PingPong"/>, which never finish.
    /// </summary>
    public bool IsFinished;

    public Tween(
        Entity target,
        TweenChannel channel,
        Vector4 from,
        Vector4 to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    )
    {
        if (!float.IsFinite(duration) || duration <= 0f)
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Tween duration must be a positive, finite number of seconds."
            );

        if (!float.IsFinite(delay) || delay < 0f)
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                "Tween delay must be a non-negative, finite number of seconds."
            );

        Target = target;
        Channel = channel;
        From = from;
        To = to;
        Duration = duration;
        Delay = delay;
        Easing = easing;
        LoopMode = loopMode;
        ElapsedTime = 0f;
        IsFinished = false;
    }

    public static Tween Create(
        Entity target,
        TweenChannel channel,
        float from,
        float to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    ) =>
        new(
            target,
            channel,
            new Vector4(from, 0f, 0f, 0f),
            new Vector4(to, 0f, 0f, 0f),
            duration,
            delay,
            easing,
            loopMode
        );

    public static Tween Create(
        Entity target,
        TweenChannel channel,
        Vector2 from,
        Vector2 to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    ) =>
        new(
            target,
            channel,
            new Vector4(from, 0f, 0f),
            new Vector4(to, 0f, 0f),
            duration,
            delay,
            easing,
            loopMode
        );

    public static Tween Create(
        Entity target,
        TweenChannel channel,
        Vector3 from,
        Vector3 to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    ) =>
        new(
            target,
            channel,
            new Vector4(from, 0f),
            new Vector4(to, 0f),
            duration,
            delay,
            easing,
            loopMode
        );

    public static Tween Create(
        Entity target,
        TweenChannel channel,
        Quaternion from,
        Quaternion to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    ) =>
        new(
            target,
            channel,
            new Vector4(from.X, from.Y, from.Z, from.W),
            new Vector4(to.X, to.Y, to.Z, to.W),
            duration,
            delay,
            easing,
            loopMode
        );

    public static Tween Create(
        Entity target,
        TweenChannel channel,
        Color from,
        Color to,
        float duration,
        float delay = 0f,
        EasingFunction easing = EasingFunction.Linear,
        TweenLoopMode loopMode = TweenLoopMode.Once
    ) => new(target, channel, from.ToVector4(), to.ToVector4(), duration, delay, easing, loopMode);
}
