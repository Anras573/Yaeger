namespace Yaeger.Graphics;

/// <summary>
/// Pure, allocation-free easing curves (standard formulas from easings.net), each mapping
/// normalized progress <c>t</c> in <c>[0, 1]</c> to an eased factor. <see cref="TweenSystem"/>
/// is the only caller in the engine, but these are plain static functions so they can be used
/// standalone (e.g. hand-rolled camera shakes) without going through a <see cref="Tween"/>.
/// </summary>
/// <remarks>
/// None of these clamp their input — callers that pass <c>t</c> outside <c>[0, 1]</c> (extrapolation)
/// get whatever the underlying formula produces, which is well-defined for every curve except the
/// zero/one special cases in <see cref="ExpoIn"/>/<see cref="ExpoOut"/>/<see cref="ExpoInOut"/> and
/// the elastic variants; <see cref="TweenSystem"/> always calls with a clamped <c>t</c>.
/// </remarks>
public static class Easing
{
    public static float Apply(EasingFunction function, float t) =>
        function switch
        {
            EasingFunction.Linear => Linear(t),
            EasingFunction.QuadIn => QuadIn(t),
            EasingFunction.QuadOut => QuadOut(t),
            EasingFunction.QuadInOut => QuadInOut(t),
            EasingFunction.CubicIn => CubicIn(t),
            EasingFunction.CubicOut => CubicOut(t),
            EasingFunction.CubicInOut => CubicInOut(t),
            EasingFunction.QuartIn => QuartIn(t),
            EasingFunction.QuartOut => QuartOut(t),
            EasingFunction.QuartInOut => QuartInOut(t),
            EasingFunction.SineIn => SineIn(t),
            EasingFunction.SineOut => SineOut(t),
            EasingFunction.SineInOut => SineInOut(t),
            EasingFunction.ExpoIn => ExpoIn(t),
            EasingFunction.ExpoOut => ExpoOut(t),
            EasingFunction.ExpoInOut => ExpoInOut(t),
            EasingFunction.BackIn => BackIn(t),
            EasingFunction.BackOut => BackOut(t),
            EasingFunction.BackInOut => BackInOut(t),
            EasingFunction.ElasticIn => ElasticIn(t),
            EasingFunction.ElasticOut => ElasticOut(t),
            EasingFunction.ElasticInOut => ElasticInOut(t),
            _ => Linear(t),
        };

    public static float Linear(float t) => t;

    public static float QuadIn(float t) => t * t;

    public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);

    public static float QuadInOut(float t) =>
        t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2) / 2f;

    public static float CubicIn(float t) => t * t * t;

    public static float CubicOut(float t) => 1f - MathF.Pow(1f - t, 3);

    public static float CubicInOut(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3) / 2f;

    public static float QuartIn(float t) => t * t * t * t;

    public static float QuartOut(float t) => 1f - MathF.Pow(1f - t, 4);

    public static float QuartInOut(float t) =>
        t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4) / 2f;

    public static float SineIn(float t) => 1f - MathF.Cos(t * MathF.PI / 2f);

    public static float SineOut(float t) => MathF.Sin(t * MathF.PI / 2f);

    public static float SineInOut(float t) => -(MathF.Cos(MathF.PI * t) - 1f) / 2f;

    public static float ExpoIn(float t) => t <= 0f ? 0f : MathF.Pow(2f, 10f * t - 10f);

    public static float ExpoOut(float t) => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);

    public static float ExpoInOut(float t)
    {
        if (t <= 0f)
            return 0f;
        if (t >= 1f)
            return 1f;
        return t < 0.5f
            ? MathF.Pow(2f, 20f * t - 10f) / 2f
            : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f;
    }

    private const float BackC1 = 1.70158f;
    private const float BackC3 = BackC1 + 1f;

    public static float BackIn(float t) => BackC3 * t * t * t - BackC1 * t * t;

    public static float BackOut(float t) =>
        1f + BackC3 * MathF.Pow(t - 1f, 3) + BackC1 * MathF.Pow(t - 1f, 2);

    private const float BackC2 = BackC1 * 1.525f;

    public static float BackInOut(float t) =>
        t < 0.5f
            ? MathF.Pow(2f * t, 2) * ((BackC2 + 1f) * 2f * t - BackC2) / 2f
            : (MathF.Pow(2f * t - 2f, 2) * ((BackC2 + 1f) * (t * 2f - 2f) + BackC2) + 2f) / 2f;

    private const float ElasticC4 = 2f * MathF.PI / 3f;

    public static float ElasticIn(float t)
    {
        if (t <= 0f)
            return 0f;
        if (t >= 1f)
            return 1f;
        return -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((t * 10f - 10.75f) * ElasticC4);
    }

    public static float ElasticOut(float t)
    {
        if (t <= 0f)
            return 0f;
        if (t >= 1f)
            return 1f;
        return MathF.Pow(2f, -10f * t) * MathF.Sin((t * 10f - 0.75f) * ElasticC4) + 1f;
    }

    private const float ElasticC5 = 2f * MathF.PI / 4.5f;

    public static float ElasticInOut(float t)
    {
        if (t <= 0f)
            return 0f;
        if (t >= 1f)
            return 1f;
        return t < 0.5f
            ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * ElasticC5)) / 2f
            : MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * ElasticC5) / 2f + 1f;
    }
}
