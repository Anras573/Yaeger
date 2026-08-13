namespace Yaeger.Graphics;

/// <summary>
/// Named easing curves applied to a tween's normalized progress (<c>t</c> in <c>[0, 1]</c>) before
/// interpolating a <see cref="Tween"/>'s channel value. See <see cref="Easing.Apply"/> and
/// docs/tweening.md.
/// </summary>
public enum EasingFunction
{
    /// <summary>No easing — progress maps directly to interpolation factor.</summary>
    Linear,
    QuadIn,
    QuadOut,
    QuadInOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    QuartIn,
    QuartOut,
    QuartInOut,
    SineIn,
    SineOut,
    SineInOut,
    ExpoIn,
    ExpoOut,
    ExpoInOut,
    BackIn,
    BackOut,
    BackInOut,
    ElasticIn,
    ElasticOut,
    ElasticInOut,
}
