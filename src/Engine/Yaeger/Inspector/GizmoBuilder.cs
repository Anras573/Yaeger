using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

/// <summary>
/// Accumulates the <see cref="GizmoLine"/> segments that make up an editor gizmo. All shapes are
/// expressed purely as line lists, so this type holds no GPU state and is safe to unit-test and to
/// reuse across frames (call <see cref="Clear"/> before rebuilding). <see cref="GizmoRenderer"/>
/// consumes <see cref="Lines"/> and draws them in world space.
/// </summary>
public sealed class GizmoBuilder
{
    private readonly List<GizmoLine> _lines = [];

    /// <summary>The line segments accumulated so far.</summary>
    public IReadOnlyList<GizmoLine> Lines => _lines;

    /// <summary>Discards all accumulated lines so the builder can be reused for the next frame.</summary>
    public void Clear() => _lines.Clear();

    /// <summary>Adds a single line segment.</summary>
    public void AddLine(Vector3 start, Vector3 end, Vector4 color) =>
        _lines.Add(new GizmoLine(start, end, color));

    // Default axis colours (X red, Y green, Z blue) used when a caller doesn't override them.
    private static readonly Vector4 DefaultAxisX = new(1f, 0.2f, 0.2f, 1f);
    private static readonly Vector4 DefaultAxisY = new(0.2f, 1f, 0.2f, 1f);
    private static readonly Vector4 DefaultAxisZ = new(0.3f, 0.5f, 1f, 1f);

    /// <summary>
    /// Adds three orientation axes of the given length, rotated by <paramref name="rotation"/> so
    /// they reflect the entity's orientation, anchored at <paramref name="origin"/>. Colours default
    /// to X red, Y green, Z blue; pass overrides to recolour them.
    /// </summary>
    public void AddAxes(
        Vector3 origin,
        Quaternion rotation,
        float length,
        Vector4? xColor = null,
        Vector4? yColor = null,
        Vector4? zColor = null
    )
    {
        var right = Vector3.Transform(Vector3.UnitX, rotation) * length;
        var up = Vector3.Transform(Vector3.UnitY, rotation) * length;
        var forward = Vector3.Transform(Vector3.UnitZ, rotation) * length;

        AddLine(origin, origin + right, xColor ?? DefaultAxisX);
        AddLine(origin, origin + up, yColor ?? DefaultAxisY);
        AddLine(origin, origin + forward, zColor ?? DefaultAxisZ);
    }

    /// <summary>
    /// Adds 2D orientation axes (X red, Y green by default) of the given <paramref name="length"/>
    /// lying in the Z = 0 plane, rotated by <paramref name="rotationRadians"/> about Z so they reflect
    /// a <see cref="Yaeger.Graphics.Transform2D"/>'s orientation, anchored at <paramref name="origin"/>.
    /// Pass colour overrides to recolour the axes.
    /// </summary>
    public void AddAxes2D(
        Vector2 origin,
        float rotationRadians,
        float length,
        Vector4? xColor = null,
        Vector4? yColor = null
    )
    {
        if (!IsFinite(origin) || !float.IsFinite(rotationRadians) || !float.IsFinite(length))
            return;

        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        var o = new Vector3(origin.X, origin.Y, 0f);

        // Rotated X/Y basis vectors in the Z = 0 plane. Colours match AddAxes (X red, Y green).
        var right = new Vector3(cos, sin, 0f) * length;
        var up = new Vector3(-sin, cos, 0f) * length;

        AddLine(o, o + right, xColor ?? DefaultAxisX);
        AddLine(o, o + up, yColor ?? DefaultAxisY);
    }

    /// <summary>
    /// Adds the four edges of a rectangle lying in the Z = 0 plane, centred at
    /// <paramref name="center"/> with the given <paramref name="halfExtents"/> and rotated by
    /// <paramref name="rotationRadians"/> about Z. Used to outline 2D sprite bounds and the
    /// <see cref="Yaeger.Graphics.Camera2D"/> viewport.
    /// </summary>
    public void AddRect(Vector2 center, Vector2 halfExtents, float rotationRadians, Vector4 color)
    {
        // Reject non-finite inputs up front, matching the other builders: a NaN/Inf would otherwise
        // flow straight into the emitted vertices.
        if (!IsFinite(center) || !IsFinite(halfExtents) || !float.IsFinite(rotationRadians))
            return;

        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);

        // Local corners (CCW), rotated into world space and lifted to Z = 0.
        Span<Vector2> local =
        [
            new(-halfExtents.X, -halfExtents.Y),
            new(halfExtents.X, -halfExtents.Y),
            new(halfExtents.X, halfExtents.Y),
            new(-halfExtents.X, halfExtents.Y),
        ];

        Span<Vector3> corners = stackalloc Vector3[4];
        for (var i = 0; i < 4; i++)
        {
            var x = local[i].X * cos - local[i].Y * sin;
            var y = local[i].X * sin + local[i].Y * cos;
            corners[i] = new Vector3(center.X + x, center.Y + y, 0f);
        }

        for (var i = 0; i < 4; i++)
            AddLine(corners[i], corners[(i + 1) % 4], color);
    }

    /// <summary>Adds an arrow from <paramref name="from"/> to <paramref name="to"/> with a small 3D arrowhead.</summary>
    public void AddArrow(Vector3 from, Vector3 to, Vector4 color, float headSize)
    {
        // Reject non-finite endpoints up front: a NaN/Inf would slip past the length check below
        // (comparisons against NaN are false) and flow into dir/Basis as NaN vertices.
        if (!IsFinite(from) || !IsFinite(to))
            return;

        AddLine(from, to, color);

        var shaft = to - from;
        var lengthSq = shaft.LengthSquared();
        if (lengthSq < 1e-12f)
            return;

        var dir = shaft / MathF.Sqrt(lengthSq);
        Basis(dir, out var u, out var v);

        var back = to - dir * headSize;
        var half = headSize * 0.5f;
        AddLine(to, back + u * half, color);
        AddLine(to, back - u * half, color);
        AddLine(to, back + v * half, color);
        AddLine(to, back - v * half, color);
    }

    /// <summary>Adds a circle centred at <paramref name="center"/> spanning the <paramref name="u"/>/<paramref name="v"/> plane.</summary>
    public void AddCircle(
        Vector3 center,
        Vector3 u,
        Vector3 v,
        float radius,
        Vector4 color,
        int segments = 24
    )
    {
        if (!float.IsFinite(radius) || radius <= 0f || segments < 3)
            return;

        var step = MathF.Tau / segments;
        var prev = center + u * radius;
        for (var i = 1; i <= segments; i++)
        {
            var a = i * step;
            var point = center + (u * MathF.Cos(a) + v * MathF.Sin(a)) * radius;
            AddLine(prev, point, color);
            prev = point;
        }
    }

    /// <summary>Adds a wireframe sphere as three orthogonal circles.</summary>
    public void AddWireSphere(Vector3 center, float radius, Vector4 color, int segments = 24)
    {
        AddCircle(center, Vector3.UnitX, Vector3.UnitY, radius, color, segments);
        AddCircle(center, Vector3.UnitY, Vector3.UnitZ, radius, color, segments);
        AddCircle(center, Vector3.UnitX, Vector3.UnitZ, radius, color, segments);
    }

    /// <summary>
    /// Adds a wireframe cone with its apex at <paramref name="apex"/>, opening along
    /// <paramref name="direction"/>: a base circle plus four lines from the apex to the rim.
    /// </summary>
    public void AddWireCone(
        Vector3 apex,
        Vector3 direction,
        float height,
        float baseRadius,
        Vector4 color,
        int segments = 24
    )
    {
        // Bail on any non-finite input before normalizing: like AddArrow, the length/height checks
        // below pass for NaN/Inf and would otherwise emit NaN vertices.
        if (
            !IsFinite(apex)
            || !IsFinite(direction)
            || !float.IsFinite(height)
            || !float.IsFinite(baseRadius)
        )
            return;

        var lengthSq = direction.LengthSquared();
        if (lengthSq < 1e-12f || height <= 0f || baseRadius < 0f)
            return;

        var dir = direction / MathF.Sqrt(lengthSq);
        Basis(dir, out var u, out var v);
        var baseCenter = apex + dir * height;

        AddCircle(baseCenter, u, v, baseRadius, color, segments);

        // Four spokes from the apex to the rim convey the cone's spread.
        AddLine(apex, baseCenter + u * baseRadius, color);
        AddLine(apex, baseCenter - u * baseRadius, color);
        AddLine(apex, baseCenter + v * baseRadius, color);
        AddLine(apex, baseCenter - v * baseRadius, color);
    }

    /// <summary>Adds the 12 edges of an axis-aligned box, each corner transformed by <paramref name="model"/>.</summary>
    public void AddTransformedBox(Aabb3D box, Matrix4x4 model, Vector4 color)
    {
        Span<Vector3> c =
        [
            new(box.Min.X, box.Min.Y, box.Min.Z),
            new(box.Max.X, box.Min.Y, box.Min.Z),
            new(box.Max.X, box.Max.Y, box.Min.Z),
            new(box.Min.X, box.Max.Y, box.Min.Z),
            new(box.Min.X, box.Min.Y, box.Max.Z),
            new(box.Max.X, box.Min.Y, box.Max.Z),
            new(box.Max.X, box.Max.Y, box.Max.Z),
            new(box.Min.X, box.Max.Y, box.Max.Z),
        ];

        for (var i = 0; i < c.Length; i++)
            c[i] = Vector3.Transform(c[i], model);

        // Bottom face, top face, then the four vertical edges connecting them.
        AddLine(c[0], c[1], color);
        AddLine(c[1], c[2], color);
        AddLine(c[2], c[3], color);
        AddLine(c[3], c[0], color);
        AddLine(c[4], c[5], color);
        AddLine(c[5], c[6], color);
        AddLine(c[6], c[7], color);
        AddLine(c[7], c[4], color);
        AddLine(c[0], c[4], color);
        AddLine(c[1], c[5], color);
        AddLine(c[2], c[6], color);
        AddLine(c[3], c[7], color);
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    // Builds an orthonormal basis (u, v) spanning the plane perpendicular to the unit vector dir.
    private static void Basis(Vector3 dir, out Vector3 u, out Vector3 v)
    {
        // Pick a reference axis that is not (near-)parallel to dir to avoid a degenerate cross.
        var reference = MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        u = Vector3.Normalize(Vector3.Cross(reference, dir));
        v = Vector3.Cross(dir, u);
    }
}
