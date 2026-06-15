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

    public static ShadowSettings Default =>
        new()
        {
            MapResolution = 2048,
            OrthographicSize = 10f,
            NearPlane = 0.1f,
            FarPlane = 50f,
            Bias = 0.005f,
            EnablePcf = true,
        };
}
