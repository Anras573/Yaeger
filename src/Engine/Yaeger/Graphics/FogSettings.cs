namespace Yaeger.Graphics;

/// <summary>
/// How <see cref="FogSettings.Density"/>/<see cref="FogSettings.Start"/>/<see cref="FogSettings.End"/>
/// combine with distance to produce a fog visibility factor.
/// </summary>
public enum FogMode
{
    /// <summary>
    /// <c>exp(-(distance * density)^2)</c> — no hard edge, thickens gradually with distance. The
    /// default: a single <see cref="FogSettings.Density"/> knob reads naturally for atmospheric
    /// haze without authoring a start/end pair.
    /// </summary>
    ExponentialSquared,

    /// <summary>
    /// Linear falloff from fully clear at <see cref="FogSettings.Start"/> to fully fogged at
    /// <see cref="FogSettings.End"/>. Worth having for authored control — e.g. a level designer
    /// hiding far-plane pop-in at a specific, predictable distance.
    /// </summary>
    Linear,
}

/// <summary>
/// Scene-wide distance fog: a depth-dependent tint mixed into every fragment <see cref="Rendering.Renderer3D"/>
/// shades, applied identically to the PBR and Blinn-Phong paths and to opaque, transparent, and
/// additive surfaces alike. Attach one to any entity; <c>MeshRenderSystem</c> picks up the first one
/// it finds each frame (the same "first X in the world" convention <see cref="AmbientLight"/> and
/// <see cref="DirectionalLight"/> use) and uploads it via <c>Renderer3D.SetFog</c>.
/// </summary>
/// <remarks>
/// Opt-in: a world with no <see cref="FogSettings"/> entity calls <c>Renderer3D.DisableFog</c>
/// instead, so a scene that never attaches one renders exactly as it did before this feature
/// existed — including a scene sharing a <c>Renderer3D</c> with one that does enable fog.
/// <para>
/// The skybox is deliberately left unfogged for this first cut — see the discussion on
/// <see href="https://github.com/Anras573/Yaeger/issues/217">issue #217</see>. Colour is a plain
/// <see cref="Graphics.Color"/> rather than something coupled to the day/night driver: a caller
/// that wants fog to warm at the horizon and cool at night should update this component's
/// <see cref="Color"/> itself (e.g. alongside <c>TimeOfDay</c>), keeping that coupling out of the
/// renderer.
/// </para>
/// </remarks>
public record struct FogSettings
{
    /// <summary>Colour fragments are mixed toward as fog visibility drops.</summary>
    public Color Color;

    /// <summary>Which falloff curve maps distance to fog visibility.</summary>
    public FogMode Mode;

    /// <summary>
    /// Thickness used by <see cref="FogMode.ExponentialSquared"/>: visibility is
    /// <c>exp(-(distance * Density)^2)</c>. Ignored by <see cref="FogMode.Linear"/>.
    /// </summary>
    public float Density;

    /// <summary>
    /// Distance at which <see cref="FogMode.Linear"/> fog begins (fragments nearer than this are
    /// unfogged). Ignored by <see cref="FogMode.ExponentialSquared"/>.
    /// </summary>
    public float Start;

    /// <summary>
    /// Distance at which <see cref="FogMode.Linear"/> fog reaches full strength (fragments farther
    /// than this are fully fog-coloured). Ignored by <see cref="FogMode.ExponentialSquared"/>.
    /// </summary>
    public float End;

    /// <summary>
    /// Light grey exponential-squared fog with a gentle density, and a 10-100 linear range for
    /// callers that switch <see cref="Mode"/>. Only used when a <see cref="FogSettings"/> is
    /// actually attached — the feature itself defaults off (see remarks).
    /// </summary>
    public static FogSettings Default =>
        new()
        {
            Color = Color.White,
            Mode = FogMode.ExponentialSquared,
            Density = 0.02f,
            Start = 10f,
            End = 100f,
        };
}
