using System.Numerics;

namespace Yaeger.Inspector;

/// <summary>
/// A single draggable translate-axis handle — a line segment from <see cref="Origin"/> to
/// <see cref="Origin"/> + <see cref="Direction"/> * <see cref="Length"/>. <see cref="AxisIndex"/> is
/// 0 (X), 1 (Y), or 2 (Z, 3D only).
/// </summary>
public readonly record struct AxisHandle(
    Vector3 Origin,
    Vector3 Direction,
    float Length,
    int AxisIndex
)
{
    /// <summary>World-space endpoint of the handle.</summary>
    public Vector3 End => Origin + Direction * Length;
}

/// <summary>
/// Computes the translate-axis handles for a selected entity — the same axis geometry
/// <see cref="GizmoBuilder.AddAxes"/> / <see cref="GizmoBuilder.AddAxes2D"/> draws, exposed as data
/// so <c>ImGuiInspector</c> can hit-test and drag them. Pure (no GPU state), so it is fully
/// unit-testable. Mirrors those methods' math exactly so a handle's hit box lines up with its drawn
/// line.
/// </summary>
public static class TranslateHandles
{
    /// <summary>Builds the X/Y/Z handles for a <see cref="Yaeger.Graphics.Transform3D"/>-driven entity.</summary>
    public static AxisHandle[] For3D(Vector3 origin, Quaternion rotation, float length)
    {
        if (!IsFinite(origin) || !IsFinite(rotation) || !float.IsFinite(length) || length <= 0f)
            return [];

        return
        [
            new AxisHandle(origin, Vector3.Transform(Vector3.UnitX, rotation), length, 0),
            new AxisHandle(origin, Vector3.Transform(Vector3.UnitY, rotation), length, 1),
            new AxisHandle(origin, Vector3.Transform(Vector3.UnitZ, rotation), length, 2),
        ];
    }

    /// <summary>
    /// Builds the X/Y handles for a <see cref="Yaeger.Graphics.Transform2D"/>-driven entity, lying in
    /// the Z = 0 plane.
    /// </summary>
    public static AxisHandle[] For2D(Vector2 origin, float rotationRadians, float length)
    {
        if (
            !IsFinite(origin)
            || !float.IsFinite(rotationRadians)
            || !float.IsFinite(length)
            || length <= 0f
        )
            return [];

        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        var o = new Vector3(origin, 0f);

        return
        [
            new AxisHandle(o, new Vector3(cos, sin, 0f), length, 0),
            new AxisHandle(o, new Vector3(-sin, cos, 0f), length, 1),
        ];
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    private static bool IsFinite(Quaternion q) =>
        float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W);
}
