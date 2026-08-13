namespace Yaeger.Graphics;

/// <summary>
/// The specific component field a <see cref="Tween"/> animates on its <see cref="Tween.Target"/>
/// entity. Deliberately a closed enum, not a reflection-based property path — see docs/tweening.md
/// for the rationale and the value packing used for each channel's <c>From</c>/<c>To</c>.
/// </summary>
public enum TweenChannel
{
    Transform2DPosition,
    Transform2DRotation,
    Transform2DScale,

    LocalTransform2DPosition,
    LocalTransform2DRotation,
    LocalTransform2DScale,

    Transform3DPosition,
    Transform3DRotation,
    Transform3DScale,

    LocalTransform3DPosition,
    LocalTransform3DRotation,
    LocalTransform3DScale,

    PointLightIntensity,
    PointLightColor,

    SpotLightIntensity,
    SpotLightColor,

    DirectionalLightIntensity,
    DirectionalLightColor,

    Material3DEmissiveColor,
    Material3DOpacity,

    Camera3DPosition,
    Camera3DTarget,
}
