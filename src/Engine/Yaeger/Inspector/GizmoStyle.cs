using System.Numerics;

namespace Yaeger.Inspector;

/// <summary>
/// Tunes the appearance of the editor selection gizmos drawn by <see cref="EntityGizmos"/> and
/// <see cref="GizmoRenderer"/>. Every value defaults to the engine's original look, so a freshly
/// constructed style reproduces today's gizmos exactly — assign a custom instance to
/// <see cref="ImGuiInspector"/>'s style to adapt the overlay to a different scene scale or visual
/// taste (e.g. raise <see cref="SizeMultiplier"/> for a 1000-unit world, or drop segment counts to
/// trade quality for cost). Runtime-only: it is not serialized with scenes.
/// </summary>
public sealed class GizmoStyle
{
    // ── Colours ───────────────────────────────────────────────────────────────

    // Axis colour defaults source from GizmoBuilder's canonical constants so the two never drift.

    /// <summary>Colour of the local +X orientation axis (3D and 2D). Default red.</summary>
    public Vector4 AxisXColor { get; set; } = GizmoBuilder.DefaultAxisX;

    /// <summary>Colour of the local +Y orientation axis (3D and 2D). Default green.</summary>
    public Vector4 AxisYColor { get; set; } = GizmoBuilder.DefaultAxisY;

    /// <summary>Colour of the local +Z orientation axis (3D only). Default blue.</summary>
    public Vector4 AxisZColor { get; set; } = GizmoBuilder.DefaultAxisZ;

    /// <summary>Colour of mesh / sprite bounds outlines. Default amber.</summary>
    public Vector4 BoundsColor { get; set; } = new(1f, 0.75f, 0.2f, 1f);

    /// <summary>Colour of the <see cref="Yaeger.Graphics.Camera2D"/> viewport and
    /// <see cref="Yaeger.Graphics.Camera3D"/> frustum. Default yellow.</summary>
    public Vector4 CameraColor { get; set; } = new(1f, 1f, 0.3f, 1f);

    /// <summary>
    /// Optional override for the directional-light gizmo colour. When <c>null</c> (the default) the
    /// gizmo uses the light's own colour, matching the original behaviour.
    /// </summary>
    public Vector4? DirectionalLightColor { get; set; }

    /// <summary>
    /// Optional override for the point-light gizmo colour. When <c>null</c> (the default) the gizmo
    /// uses the light's own colour.
    /// </summary>
    public Vector4? PointLightColor { get; set; }

    /// <summary>
    /// Optional override for the spot-light gizmo colour. When <c>null</c> (the default) the gizmo
    /// uses the light's own colour.
    /// </summary>
    public Vector4? SpotLightColor { get; set; }

    // ── Sizes / scale factors ─────────────────────────────────────────────────

    /// <summary>
    /// Global multiplier applied to every fixed-size gizmo element (orientation axes, the directional
    /// light's arrows / ray spread / sun marker, and the point-light position core). Range-driven
    /// shapes that visualise a real quantity — the point-light reach sphere and the spot-light cone —
    /// are <em>not</em> scaled, so they keep showing the light's actual extent. Raise this for large
    /// worlds where the default 0.5-unit axes are invisible. Default 1.
    /// </summary>
    public float SizeMultiplier { get; set; } = 1f;

    /// <summary>Length of the 3D orientation axes before <see cref="SizeMultiplier"/>. Default 0.5.</summary>
    public float AxisLength { get; set; } = 0.5f;

    /// <summary>Length of the 2D orientation axes before <see cref="SizeMultiplier"/>. Default 0.5.</summary>
    public float Axis2DLength { get; set; } = 0.5f;

    /// <summary>Length of each directional-light ray arrow before <see cref="SizeMultiplier"/>. Default 1.5.</summary>
    public float DirectionalArrowLength { get; set; } = 1.5f;

    /// <summary>Size of the arrowheads on directional-light rays before <see cref="SizeMultiplier"/>. Default 0.18.</summary>
    public float ArrowHeadSize { get; set; } = 0.18f;

    /// <summary>Spread of the parallel directional-light rays before <see cref="SizeMultiplier"/>. Default 0.18.</summary>
    public float DirectionalRaySpread { get; set; } = 0.18f;

    /// <summary>Radius of the directional-light "sun" marker before <see cref="SizeMultiplier"/>. Default 0.15.</summary>
    public float DirectionalSunRadius { get; set; } = 0.15f;

    /// <summary>
    /// Size of the small point-light position core before <see cref="SizeMultiplier"/>. The drawn
    /// core is capped at a tenth of the light's range so it never swamps a tiny light. Default 0.1.
    /// </summary>
    public float PointLightCoreSize { get; set; } = 0.1f;

    // ── Segment counts (quality vs. cost) ─────────────────────────────────────

    /// <summary>Segment count for the point-light reach sphere. Default 24.</summary>
    public int SphereSegments { get; set; } = 24;

    /// <summary>Segment count for the directional-light "sun" marker sphere. Default 16.</summary>
    public int SunSphereSegments { get; set; } = 16;

    /// <summary>Segment count for the small point-light position core. Default 12.</summary>
    public int PointLightCoreSegments { get; set; } = 12;

    /// <summary>Segment count for the spot-light cone base circle. Default 24.</summary>
    public int ConeSegments { get; set; } = 24;

    // ── Rendering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>false</c> (the default) gizmos draw on top of the scene regardless of occluding
    /// geometry — handy for finding a light tucked behind a wall. Set <c>true</c> to depth-test
    /// gizmos against the scene so they are hidden by geometry in front of them.
    /// </summary>
    public bool DepthTest { get; set; }

    /// <summary>Width of the gizmo lines in pixels (driver-dependent clamping applies). Default 1.</summary>
    public float LineWidth { get; set; } = 1f;
}
