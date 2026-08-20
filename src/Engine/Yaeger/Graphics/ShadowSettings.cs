namespace Yaeger.Graphics;

/// <summary>
/// Configuration for directional-light shadow mapping. Controls the shadow map's resolution, the
/// light's orthographic frustum, the depth bias used to fight shadow acne, and whether a 3×3 PCF
/// kernel softens the shadow edges.
/// </summary>
public record struct ShadowSettings
{
    /// <summary>Square shadow map dimension in texels (e.g. 2048).</summary>
    public int MapResolution;

    /// <summary>Half-extent of the light's orthographic frustum, in world units.</summary>
    public float OrthographicSize;

    /// <summary>Near plane of the light's orthographic projection.</summary>
    public float NearPlane;

    /// <summary>Far plane of the light's orthographic projection.</summary>
    public float FarPlane;

    /// <summary>Depth bias subtracted during the shadow test to prevent shadow acne.</summary>
    public float Bias;

    /// <summary>When true, a 3×3 PCF (percentage-closer filtering) kernel softens shadow edges.</summary>
    public bool EnablePcf;

    /// <summary>
    /// When true, the light's frustum is fitted to the shadow casters' bounding sphere each frame
    /// instead of using <see cref="OrthographicSize"/>/<see cref="NearPlane"/>/<see cref="FarPlane"/>.
    /// </summary>
    /// <remarks>
    /// A hand-tuned extent only holds for the light angle it was tuned at: a sun near the horizon
    /// casts shadows far longer than the extent that frames it at noon, and casters fall out of the
    /// frustum — their shadows simply vanish. A sphere fit covers the scene from every direction, so
    /// it holds through a full arc. The cost is a pass over the <see cref="Aabb3D"/> store each
    /// frame and, for a scene much wider than the shadow map's texel density, blockier shadows than
    /// a tight hand-tuned extent gives. Off by default so existing scenes keep the extent they were
    /// tuned with.
    /// </remarks>
    public bool AutoFit;

    /// <summary>
    /// Light elevation (the Y component of its direction, i.e. the sine of its altitude) below which
    /// shadows fade out, reaching zero strength at the horizon. Zero or negative gives a hard cut at
    /// the horizon with no fade.
    /// </summary>
    /// <remarks>
    /// A light at or below the horizon casts no shadow at all: it lights nothing a shadow would fall
    /// on, and its shadow map degenerates as the frustum flattens along the horizon. The fade band
    /// spreads that transition over a few degrees so a setting sun dims its shadows out instead of
    /// dropping them in one frame.
    /// </remarks>
    public float HorizonFadeElevation;

    public static ShadowSettings Default =>
        new()
        {
            MapResolution = 2048,
            OrthographicSize = 10f,
            NearPlane = 0.1f,
            FarPlane = 50f,
            Bias = 0.005f,
            EnablePcf = true,
            AutoFit = false,
            HorizonFadeElevation = 0.05f,
        };
}
