using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class ViewportPickingTests
{
    private static bool Near(float a, float b, float epsilon = 1e-3f) => MathF.Abs(a - b) < epsilon;

    // ── TryGetPickRay ──────────────────────────────────────────────────────────

    [Fact]
    public void TryGetPickRay_IdentityViewProj_ReturnsRayAlongPositiveZ()
    {
        // With viewProj = Identity, clip space == world space, so unprojecting NDC z = 0 and z = 1
        // at the same (x, y) yields world points (x, y, 0) and (x, y, 1) — a ray straight along +Z.
        var ok = ViewportPicking.TryGetPickRay(
            Vector2.Zero,
            Matrix4x4.Identity,
            out var origin,
            out var direction
        );

        Assert.True(ok);
        Assert.Equal(Vector3.Zero, origin);
        Assert.Equal(new Vector3(0, 0, 1), direction);
    }

    [Fact]
    public void TryGetPickRay_PerspectiveCamera_PointsTowardTarget()
    {
        var camera = new Camera3D(
            new Vector3(0, 0, 5),
            Vector3.Zero,
            Vector3.UnitY,
            MathF.PI / 2f,
            0.1f,
            100f
        );
        var viewProj = camera.ViewMatrix * camera.ProjectionMatrix(1f);

        var ok = ViewportPicking.TryGetPickRay(Vector2.Zero, viewProj, out _, out var direction);

        Assert.True(ok);
        // Screen-center ray should point from the camera straight at its target (0,0,0), i.e. -Z.
        Assert.True(Vector3.Dot(direction, new Vector3(0, 0, -1)) > 0.999f);
    }

    [Fact]
    public void TryGetPickRay_SingularMatrix_ReturnsFalse()
    {
        var ok = ViewportPicking.TryGetPickRay(
            Vector2.Zero,
            default,
            out var origin,
            out var direction
        );

        Assert.False(ok);
        Assert.Equal(Vector3.Zero, origin);
        Assert.Equal(Vector3.Zero, direction);
    }

    [Fact]
    public void TryGetPickRay_NonFiniteNdc_ReturnsFalse()
    {
        Assert.False(
            ViewportPicking.TryGetPickRay(
                new Vector2(float.NaN, 0f),
                Matrix4x4.Identity,
                out _,
                out _
            )
        );
    }

    // ── TryUnprojectPoint2D ────────────────────────────────────────────────────

    [Fact]
    public void TryUnprojectPoint2D_ScreenCenter_ReturnsCameraPosition()
    {
        var camera = new Camera2D(new Vector2(5, 3), Zoom: 2f);
        var viewProj = camera.ViewProjection(16f / 9f);

        var ok = ViewportPicking.TryUnprojectPoint2D(Vector2.Zero, viewProj, out var world);

        Assert.True(ok);
        Assert.True(Near(world.X, 5f));
        Assert.True(Near(world.Y, 3f));
    }

    [Fact]
    public void TryUnprojectPoint2D_SingularMatrix_ReturnsFalse()
    {
        Assert.False(ViewportPicking.TryUnprojectPoint2D(Vector2.Zero, default, out _));
    }

    // ── WorldToScreen ──────────────────────────────────────────────────────────

    [Fact]
    public void WorldToScreen_IdentityViewProj_OriginMapsToScreenCenter()
    {
        var screen = ViewportPicking.WorldToScreen(
            Vector3.Zero,
            Matrix4x4.Identity,
            new Vector2(800, 600)
        );

        Assert.Equal(new Vector2(400, 300), screen);
    }

    [Fact]
    public void WorldToScreen_IdentityViewProj_PositiveNdcYMapsToScreenTop()
    {
        // NDC is Y-up (bottom-origin); screen space is Y-down (top-origin) — a positive world/NDC Y
        // must map to the smaller (top) screen Y, matching Mouse.Position's convention.
        var screen = ViewportPicking.WorldToScreen(
            new Vector3(0, 1, 0),
            Matrix4x4.Identity,
            new Vector2(800, 600)
        );

        Assert.Equal(0f, screen.Y);
    }

    // ── TryIntersectRayAabb ────────────────────────────────────────────────────

    [Fact]
    public void TryIntersectRayAabb_RayHitsBoxFromOutside_ReturnsEntryDistance()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = ViewportPicking.TryIntersectRayAabb(
            new Vector3(0, 0, 5),
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out var distance
        );

        Assert.True(hit);
        Assert.True(Near(distance, 4f));
    }

    [Fact]
    public void TryIntersectRayAabb_RayPointsAway_ReturnsFalse()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = ViewportPicking.TryIntersectRayAabb(
            new Vector3(0, 0, 5),
            new Vector3(0, 0, 1),
            box,
            Matrix4x4.Identity,
            out _
        );

        Assert.False(hit);
    }

    [Fact]
    public void TryIntersectRayAabb_RayMissesSideways_ReturnsFalse()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = ViewportPicking.TryIntersectRayAabb(
            new Vector3(10, 0, 5),
            new Vector3(0, 0, -1),
            box,
            Matrix4x4.Identity,
            out _
        );

        Assert.False(hit);
    }

    [Fact]
    public void TryIntersectRayAabb_TransformedModel_HitsTranslatedBox()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));
        var model = Matrix4x4.CreateTranslation(5, 0, 0);

        var hit = ViewportPicking.TryIntersectRayAabb(
            new Vector3(5, 0, 5),
            new Vector3(0, 0, -1),
            box,
            model,
            out var distance
        );

        Assert.True(hit);
        Assert.True(Near(distance, 4f));
    }

    [Fact]
    public void TryIntersectRayAabb_SingularModel_ReturnsFalse()
    {
        var box = new Aabb3D(new Vector3(-1), new Vector3(1));

        var hit = ViewportPicking.TryIntersectRayAabb(
            Vector3.Zero,
            new Vector3(0, 0, -1),
            box,
            default,
            out _
        );

        Assert.False(hit);
    }

    // ── TryClosestPointOnAxisToRay ─────────────────────────────────────────────

    [Fact]
    public void TryClosestPointOnAxisToRay_RayCrossesAxis_ReturnsExactCrossingParam()
    {
        // The X axis through the origin; a ray straight down through (3, 5, 0) crosses it at x = 3.
        var ok = ViewportPicking.TryClosestPointOnAxisToRay(
            Vector3.Zero,
            Vector3.UnitX,
            new Vector3(3, 5, 0),
            new Vector3(0, -1, 0),
            out var axisParam
        );

        Assert.True(ok);
        Assert.True(Near(axisParam, 3f));
    }

    [Fact]
    public void TryClosestPointOnAxisToRay_ParallelRay_FallsBackToProjection()
    {
        // Ray direction parallel to the axis: the skew-line solution is undefined (denom ~ 0), so
        // this falls back to projecting the ray origin's offset onto the axis.
        var ok = ViewportPicking.TryClosestPointOnAxisToRay(
            Vector3.Zero,
            Vector3.UnitY,
            new Vector3(2, 0, 0),
            Vector3.UnitY,
            out var axisParam
        );

        Assert.True(ok);
        Assert.True(Near(axisParam, 0f));
    }

    [Fact]
    public void TryClosestPointOnAxisToRay_NonFiniteInput_ReturnsFalse()
    {
        var ok = ViewportPicking.TryClosestPointOnAxisToRay(
            new Vector3(float.NaN, 0, 0),
            Vector3.UnitX,
            Vector3.Zero,
            Vector3.UnitZ,
            out _
        );

        Assert.False(ok);
    }

    // ── ClosestPointOnAxis2D ───────────────────────────────────────────────────

    [Fact]
    public void ClosestPointOnAxis2D_ProjectsPointOntoAxis()
    {
        var param = ViewportPicking.ClosestPointOnAxis2D(
            Vector2.Zero,
            Vector2.UnitX,
            new Vector2(5, 3)
        );

        Assert.True(Near(param, 5f));
    }

    // ── DistancePointToSegment ─────────────────────────────────────────────────

    [Fact]
    public void DistancePointToSegment_PerpendicularToMidpoint_ReturnsPerpendicularDistance()
    {
        var distance = ViewportPicking.DistancePointToSegment(
            new Vector2(5, 5),
            Vector2.Zero,
            new Vector2(10, 0)
        );

        Assert.True(Near(distance, 5f));
    }

    [Fact]
    public void DistancePointToSegment_BeyondEndpoint_ClampsToNearestEndpoint()
    {
        var distance = ViewportPicking.DistancePointToSegment(
            new Vector2(-5, 0),
            Vector2.Zero,
            new Vector2(10, 0)
        );

        Assert.True(Near(distance, 5f));
    }

    // ── PointInOrientedRect ────────────────────────────────────────────────────

    [Fact]
    public void PointInOrientedRect_AxisAligned_ClassifiesInsideAndOutside()
    {
        Assert.True(
            ViewportPicking.PointInOrientedRect(
                new Vector2(1, 0.5f),
                Vector2.Zero,
                new Vector2(2, 1),
                0f
            )
        );
        Assert.False(
            ViewportPicking.PointInOrientedRect(
                new Vector2(3, 0),
                Vector2.Zero,
                new Vector2(2, 1),
                0f
            )
        );
    }

    [Fact]
    public void PointInOrientedRect_MatchesGizmoBuilderAddRectCorners()
    {
        // Cross-validate against the actual gizmo geometry: every corner GizmoBuilder.AddRect draws
        // must classify as inside (on the boundary), and a point well outside the rotated rect must
        // classify as outside.
        var builder = new GizmoBuilder();
        var center = new Vector2(1, 2);
        var halfExtents = new Vector2(3, 1);
        var rotation = MathF.PI / 6f;

        builder.AddRect(center, halfExtents, rotation, Vector4.One);

        var corners = builder
            .Lines.Select(l => new Vector2(l.Start.X, l.Start.Y))
            .Distinct()
            .ToArray();
        Assert.NotEmpty(corners);

        foreach (var corner in corners)
        {
            // Slightly inflate the half-extents to absorb floating point rounding at the boundary.
            var inflated = halfExtents + new Vector2(1e-3f, 1e-3f);
            Assert.True(ViewportPicking.PointInOrientedRect(corner, center, inflated, rotation));
        }

        Assert.False(
            ViewportPicking.PointInOrientedRect(
                new Vector2(100, 100),
                center,
                halfExtents,
                rotation
            )
        );
    }
}
