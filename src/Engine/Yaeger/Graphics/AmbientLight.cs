namespace Yaeger.Graphics;

/// <summary>
/// Scene-wide ambient light: the flat term added to every PBR-shaded fragment to stand in for
/// light that arrives from everywhere rather than from a specific source. Attach one to any entity;
/// <c>MeshRenderSystem</c> picks up the first one it finds each frame (the same "first X in the
/// world" convention <see cref="DirectionalLight"/> and <see cref="Camera3D"/> use) and uploads it
/// to <c>Renderer3D</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two things this does <em>not</em> affect:
/// </para>
/// <para>
/// <b>Image-based lighting.</b> When a scene's skybox has a registered <c>EnvironmentMap</c>, the
/// PBR path derives its ambient from that instead — the sky already encodes where the light comes
/// from, which is strictly better than one flat colour. This component is the fallback for scenes
/// without IBL, and is ignored while IBL is active.
/// </para>
/// <para>
/// <b>The Blinn-Phong path.</b> That path's ambient is per-material
/// (<see cref="Material3D.Ambient"/>), not scene-wide, and predates this component; folding a
/// scene-wide term into it would change the appearance of every existing Blinn-Phong scene. A
/// Blinn-Phong scene that wants an ambient that varies over time should drive
/// <see cref="Material3D.Ambient"/> (via <see cref="Tween"/> or directly) or opt into
/// <see cref="Material3D.UsePbr"/>.
/// </para>
/// </remarks>
public record struct AmbientLight
{
    public Color Color;

    /// <summary>
    /// Multiplier applied to <see cref="Color"/>. Kept separate from the colour because
    /// <see cref="Graphics.Color"/> channels are byte-based and can't exceed 1.0, so this is how an
    /// ambient brighter than the colour itself is authored — and how a day/night cycle dims one
    /// palette across several stops without quantising it to bytes on the way.
    /// </summary>
    public float Intensity;

    /// <summary>
    /// White at 0.03 — the constant the PBR shader used before this component existed, so a scene
    /// that never attaches an <see cref="AmbientLight"/> renders exactly as it did.
    /// </summary>
    public static AmbientLight Default => new() { Color = Color.White, Intensity = 0.03f };
}
