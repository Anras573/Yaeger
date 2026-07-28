namespace Yaeger.Graphics;

/// <summary>
/// How a <see cref="Material3D"/> is composited by <see cref="Rendering.Renderer3D"/> and
/// <see cref="Systems.MeshRenderSystem"/>. See docs/pbr.md for the rendering details of each mode.
/// </summary>
public enum MaterialBlendMode
{
    /// <summary>
    /// Fully opaque. Drawn in the main pass with depth write on. The default, and the only mode
    /// that existed before transparency support was added — every material defaults to this so
    /// existing scenes are unaffected.
    /// </summary>
    Opaque = 0,

    /// <summary>
    /// Alpha-tested. Drawn in the main pass (depth write on, same as <see cref="Opaque"/>) but
    /// fragments whose alpha falls below <see cref="Material3D.AlphaCutoff"/> are discarded
    /// rather than blended, so no back-to-front sorting is required. Suited to foliage, fences,
    /// and similar cutout geometry.
    /// </summary>
    Cutout = 1,

    /// <summary>
    /// Alpha-blended. Drawn in a separate pass, after every opaque/cutout material, sorted
    /// back-to-front by view-space depth, with depth testing on but depth writes off. Receives
    /// lighting and shadows like an opaque material but does not itself cast shadows.
    /// </summary>
    Transparent = 2,
}
