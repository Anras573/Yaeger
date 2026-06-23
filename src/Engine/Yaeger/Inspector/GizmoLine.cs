using System.Numerics;

namespace Yaeger.Inspector;

/// <summary>
/// A single coloured line segment in world space, the only primitive the gizmo overlay draws.
/// Boxes, spheres, cones and arrows are all decomposed into a list of these by
/// <see cref="GizmoBuilder"/> and uploaded to the GPU by <see cref="GizmoRenderer"/>.
/// </summary>
/// <param name="Start">World-space start point.</param>
/// <param name="End">World-space end point.</param>
/// <param name="Color">RGBA colour (0-1 per channel).</param>
public readonly record struct GizmoLine(Vector3 Start, Vector3 End, Vector4 Color);
