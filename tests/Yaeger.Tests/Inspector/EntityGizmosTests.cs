using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class EntityGizmosTests
{
    private const float Aspect = 16f / 9f;

    private static IReadOnlyList<GizmoLine> Build(
        World world,
        Entity entity,
        GizmoStyle? style = null
    )
    {
        var builder = new GizmoBuilder();
        EntityGizmos.Build(world, entity, Aspect, builder, style);
        return builder.Lines;
    }

    [Fact]
    public void EntityWithNoVisualComponents_ProducesNoGizmos()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.Empty(Build(world, entity));
    }

    [Fact]
    public void Transform2D_DrawsAxesAndScaledBounds()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // Position (2,3), no rotation, scale (4,2) → the sprite quad spans X∈[0,4], Y∈[2,4].
        world.AddComponent(entity, new Transform2D(new Vector2(2, 3), 0f, new Vector2(4, 2)));

        var lines = Build(world, entity);

        // Two axes (X, Y) + four bounds edges.
        Assert.Equal(6, lines.Count);
        // Axes anchored at the entity position.
        Assert.Contains(lines, l => l.Start == new Vector3(2, 3, 0));
        // Bounds reach the scaled sprite corners (half-extents 2 × 1 → X = 4, Y = 4).
        Assert.Contains(lines, l => Near(l.Start.X, 4f) || Near(l.End.X, 4f));
        Assert.Contains(lines, l => Near(l.Start.Y, 4f) || Near(l.End.Y, 4f));
        // 2D gizmos live in the Z = 0 plane.
        Assert.All(
            lines,
            l =>
            {
                Assert.Equal(0f, l.Start.Z);
                Assert.Equal(0f, l.End.Z);
            }
        );
        // Bounds rectangle is amber.
        Assert.Contains(lines, l => l.Color == new Vector4(1f, 0.75f, 0.2f, 1f));
    }

    [Fact]
    public void Transform2D_RotationOrientsAxesAndBounds()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // 90° rotation maps the local +X axis onto world +Y.
        world.AddComponent(entity, new Transform2D(Vector2.Zero, MathF.PI / 2f, new Vector2(2, 1)));

        var lines = Build(world, entity);

        // The red X axis (first line) now points up the Y axis.
        var xAxis = lines[0];
        Assert.True(xAxis.Color.X > xAxis.Color.Y && xAxis.Color.X > xAxis.Color.Z);
        Assert.True(Near(xAxis.End.X, 0f) && xAxis.End.Y > 0.4f);
    }

    [Fact]
    public void Camera2D_DrawsViewportRectangle()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Camera2D(Vector2.Zero, 1f, 0f));

        var lines = Build(world, entity);

        // A single rectangle (4 edges), coloured yellow.
        Assert.Equal(4, lines.Count);
        Assert.All(lines, l => Assert.Equal(new Vector4(1f, 1f, 0.3f, 1f), l.Color));
        // At zoom 1 the visible span is [-aspect, aspect] × [-1, 1].
        var maxX = lines.Max(l => MathF.Max(MathF.Abs(l.Start.X), MathF.Abs(l.End.X)));
        var maxY = lines.Max(l => MathF.Max(MathF.Abs(l.Start.Y), MathF.Abs(l.End.Y)));
        Assert.Equal(Aspect, maxX, 3);
        Assert.Equal(1f, maxY, 3);
    }

    [Fact]
    public void Camera2D_ZoomNarrowsViewport()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Camera2D(Vector2.Zero, 2f, 0f));

        var lines = Build(world, entity);

        // Zoom 2 halves the visible span: [-aspect/2, aspect/2] × [-0.5, 0.5].
        var maxX = lines.Max(l => MathF.Max(MathF.Abs(l.Start.X), MathF.Abs(l.End.X)));
        var maxY = lines.Max(l => MathF.Max(MathF.Abs(l.Start.Y), MathF.Abs(l.End.Y)));
        Assert.Equal(Aspect / 2f, maxX, 3);
        Assert.Equal(0.5f, maxY, 3);
    }

    [Fact]
    public void Camera2D_DefaultZeroZoom_ProducesFiniteViewport()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // default(Camera2D) has Zoom = 0, which Camera2D.ViewProjection treats as 1.
        world.AddComponent(entity, default(Camera2D));

        var lines = Build(world, entity);

        Assert.Equal(4, lines.Count);
        Assert.All(
            lines,
            l =>
            {
                Assert.True(float.IsFinite(l.Start.X) && float.IsFinite(l.Start.Y));
                Assert.True(float.IsFinite(l.End.X) && float.IsFinite(l.End.Y));
            }
        );
    }

    [Fact]
    public void Camera2D_InfiniteZoom_CollapsesToFinitePoint()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // Match Camera2D.ViewProjection: a +Infinity zoom is kept, collapsing the visible span to a
        // point (aspect/∞ = 0). The emitted vertices must stay finite — and all coincide at the
        // camera position.
        world.AddComponent(entity, new Camera2D(new Vector2(3, 4), float.PositiveInfinity, 0f));

        var lines = Build(world, entity);

        Assert.Equal(4, lines.Count);
        Assert.All(
            lines,
            l =>
            {
                Assert.Equal(new Vector3(3, 4, 0), l.Start);
                Assert.Equal(new Vector3(3, 4, 0), l.End);
            }
        );
    }

    [Fact]
    public void MixedDimensions_EmitsOnly2DGizmos()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // An entity carrying both 2D and 3D components (the Add Component menu allows this) is
        // treated as 2D for gizmos, matching the 2D projection the inspector would pick — so the
        // far-off 3D axes must not be emitted with a 2D view-projection.
        world.AddComponent(entity, new Transform2D(Vector2.Zero, 0f, Vector2.One));
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(50, 0, 0), Quaternion.Identity, Vector3.One)
        );

        var lines = Build(world, entity);

        // Only the 2D gizmos (2 axes + 4 bounds edges); the 3D axes anchored at X = 50 are skipped.
        Assert.Equal(6, lines.Count);
        Assert.DoesNotContain(lines, l => l.Start.X > 1f || l.End.X > 1f);
    }

    [Fact]
    public void Transform3D_DrawsOrientationAxes()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(5, 0, 0), Quaternion.Identity, Vector3.One)
        );

        var lines = Build(world, entity);

        // Three axes, all anchored at the entity position.
        Assert.True(lines.Count >= 3);
        Assert.Contains(lines, l => l.Start == new Vector3(5, 0, 0));
    }

    [Fact]
    public void Mesh_DrawsBoundingBoxAtWorldPosition()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Transform3D(new Vector3(10, 0, 0), Quaternion.Identity, Vector3.One)
        );
        world.AddComponent(entity, new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1)));

        var lines = Build(world, entity);

        // Verify the actual box extents are present (min X = 9, max X = 11). The 0.5-length
        // Transform3D axes only reach X = 10.5, so hitting 9 and 11 proves the transformed Aabb3D
        // bounds are drawn rather than just the axes.
        Assert.Contains(lines, l => Near(l.Start.X, 9f) || Near(l.End.X, 9f));
        Assert.Contains(lines, l => Near(l.Start.X, 11f) || Near(l.End.X, 11f));
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-3f;

    [Fact]
    public void DirectionalLight_DrawsRaysAlongTravelDirection()
    {
        var world = new World();
        var entity = world.CreateEntity();
        // Direction points toward the source (up), so light travels downward.
        world.AddComponent(
            entity,
            new DirectionalLight
            {
                Direction = Vector3.UnitY,
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var lines = Build(world, entity);

        Assert.NotEmpty(lines);
        // At least one ray shaft should point downward (travel = -Direction).
        Assert.Contains(
            lines,
            l =>
            {
                var d = l.End - l.Start;
                return d.LengthSquared() > 0.5f && Vector3.Normalize(d).Y < -0.9f;
            }
        );
        // Coloured by the light's colour (white, opaque).
        Assert.Contains(lines, l => l.Color == new Vector4(1, 1, 1, 1));
    }

    [Fact]
    public void DirectionalLight_WithoutTransform_AnchorsAtOrigin()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, DirectionalLight.Default);

        var lines = Build(world, entity);

        Assert.NotEmpty(lines);
        // The sun marker sphere is centred on the origin, so points stay near it.
        Assert.All(lines, l => Assert.True(l.Start.Length() < 5f));
    }

    [Fact]
    public void PointLight_DrawsRangeSphere()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            entity,
            new PointLight
            {
                Color = Color.Red,
                Intensity = 1f,
                Range = 5f,
            }
        );

        var lines = Build(world, entity);

        // The widest gizmo extent should match the light's range.
        var maxExtent = lines.Max(l => MathF.Max(l.Start.Length(), l.End.Length()));
        Assert.Equal(5f, maxExtent, 2);
        Assert.Contains(lines, l => l.Color == new Vector4(1, 0, 0, 1));
    }

    [Fact]
    public void PointLight_ZeroRange_DrawsOnlyPositionCoreNoReachSphere()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        // Range 0 disables the light in the renderer, so no reach sphere should be drawn.
        world.AddComponent(
            entity,
            new PointLight
            {
                Color = Color.Red,
                Intensity = 1f,
                Range = 0f,
            }
        );

        var lines = Build(world, entity);

        // Only the small position core remains (axes aside), so nothing extends near a 1-unit reach.
        var maxExtent = lines.Max(l => MathF.Max(l.Start.Length(), l.End.Length()));
        Assert.True(maxExtent < 0.6f, $"expected a small core, but a line reached {maxExtent}");
    }

    [Fact]
    public void SpotLight_ZeroRange_DrawsNoCone()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            entity,
            new SpotLight
            {
                Color = Color.White,
                Intensity = 1f,
                Direction = -Vector3.UnitY,
                InnerConeAngle = MathF.PI / 12f,
                OuterConeAngle = MathF.PI / 6f,
                Range = 0f,
            }
        );

        var lines = Build(world, entity);

        // Only the Transform3D axes remain (length 0.5); the disabled cone is skipped.
        Assert.All(lines, l => Assert.True(l.End.Length() <= 0.5f + 1e-3f));
    }

    [Fact]
    public void SpotLight_NonFiniteOuterAngle_ProducesFiniteVertices()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            entity,
            new SpotLight
            {
                Color = Color.White,
                Intensity = 1f,
                Direction = -Vector3.UnitY,
                InnerConeAngle = 0f,
                OuterConeAngle = float.NaN,
                Range = 5f,
            }
        );

        var lines = Build(world, entity);

        Assert.NotEmpty(lines);
        Assert.All(
            lines,
            l =>
            {
                Assert.True(
                    float.IsFinite(l.Start.X)
                        && float.IsFinite(l.Start.Y)
                        && float.IsFinite(l.Start.Z)
                );
                Assert.True(
                    float.IsFinite(l.End.X) && float.IsFinite(l.End.Y) && float.IsFinite(l.End.Z)
                );
            }
        );
    }

    [Fact]
    public void SpotLight_DrawsConeReachingItsRange()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            entity,
            new SpotLight
            {
                Color = Color.White,
                Intensity = 1f,
                Direction = -Vector3.UnitY,
                InnerConeAngle = MathF.PI / 12f,
                OuterConeAngle = MathF.PI / 6f,
                Range = 8f,
            }
        );

        var lines = Build(world, entity);

        Assert.NotEmpty(lines);
        // The cone base lies at range distance along the beam (downward).
        Assert.Contains(lines, l => l.Start.Y <= -7.9f || l.End.Y <= -7.9f);
    }

    [Fact]
    public void Camera3D_DrawsFrustum()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Camera3D(
                new Vector3(0, 0, 5),
                Vector3.Zero,
                Vector3.UnitY,
                MathF.PI / 4f,
                0.1f,
                100f
            )
        );

        var lines = Build(world, entity);

        // Near + far rectangles (4 edges each) plus 4 connecting edges = 12 lines.
        Assert.Equal(12, lines.Count);
        Assert.All(lines, l => Assert.Equal(new Vector4(1, 1, 0.3f, 1), l.Color));
    }

    // ── Style configurability (issue #123) ────────────────────────────────────

    [Fact]
    public void Style_CustomAxisColors_FlowIntoTransform3DAxes()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));

        var style = new GizmoStyle
        {
            AxisXColor = new Vector4(0.9f, 0f, 0f, 1f),
            AxisYColor = new Vector4(0f, 0.9f, 0f, 1f),
            AxisZColor = new Vector4(0f, 0f, 0.9f, 1f),
        };

        var lines = Build(world, entity, style);

        Assert.Equal(style.AxisXColor, lines[0].Color);
        Assert.Equal(style.AxisYColor, lines[1].Color);
        Assert.Equal(style.AxisZColor, lines[2].Color);
    }

    [Fact]
    public void Style_SizeMultiplier_ScalesAxisLength()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));

        // AxisLength 0.5 × multiplier 4 → 2-unit axes.
        var style = new GizmoStyle { SizeMultiplier = 4f };

        var lines = Build(world, entity, style);

        var maxAxisReach = MathF.Max(MathF.Max(lines[0].End.X, lines[1].End.Y), lines[2].End.Z);
        Assert.Equal(2f, maxAxisReach, 3);
    }

    [Fact]
    public void Style_CustomBoundsColor_FlowsIntoMeshBox()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(entity, new Aabb3D(new Vector3(-1, -1, -1), new Vector3(1, 1, 1)));

        var bounds = new Vector4(0.2f, 0.4f, 0.6f, 1f);
        var style = new GizmoStyle { BoundsColor = bounds };

        var lines = Build(world, entity, style);

        // The 12 box edges carry the custom bounds colour (axes aside).
        Assert.Equal(12, lines.Count(l => l.Color == bounds));
    }

    [Fact]
    public void Style_CustomCameraColor_FlowsIntoFrustum()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Camera3D(
                new Vector3(0, 0, 5),
                Vector3.Zero,
                Vector3.UnitY,
                MathF.PI / 4f,
                0.1f,
                100f
            )
        );

        var cameraColor = new Vector4(0.3f, 0.6f, 0.9f, 1f);
        var style = new GizmoStyle { CameraColor = cameraColor };

        var lines = Build(world, entity, style);

        Assert.All(lines, l => Assert.Equal(cameraColor, l.Color));
    }

    [Fact]
    public void Style_DirectionalLightColorOverride_ReplacesLightColor()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new DirectionalLight
            {
                Direction = Vector3.UnitY,
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var overrideColor = new Vector4(0.1f, 0.2f, 0.3f, 1f);
        var style = new GizmoStyle { DirectionalLightColor = overrideColor };

        var lines = Build(world, entity, style);

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(overrideColor, l.Color));
    }

    [Fact]
    public void Style_PointLightSphereSegments_ControlLineCount()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));
        world.AddComponent(
            entity,
            new PointLight
            {
                Color = Color.Red,
                Intensity = 1f,
                Range = 5f,
            }
        );

        var style = new GizmoStyle { SphereSegments = 8, PointLightCoreSegments = 6 };

        var lines = Build(world, entity, style);

        // Reach sphere (3 circles × 8) + core (3 × 6) + 3 Transform3D axes.
        Assert.Equal(8 * 3 + 6 * 3 + 3, lines.Count);
    }

    [Fact]
    public void Style_SizeMultiplier_ScalesDirectionalArrowLength()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new DirectionalLight
            {
                Direction = Vector3.UnitY,
                Color = Color.White,
                Intensity = 1f,
            }
        );

        var style = new GizmoStyle { SizeMultiplier = 2f };

        var lines = Build(world, entity, style);

        // Arrow length 1.5 × 2 = 3; a ray shaft (downward travel) should reach roughly that far.
        var longestShaft = lines.Max(l => (l.End - l.Start).Length());
        Assert.True(
            longestShaft >= 3f - 1e-3f,
            $"expected a ~3-unit ray, longest was {longestShaft}"
        );
    }

    [Fact]
    public void Style_NonFiniteSizeMultiplier_FallsBackToFiniteDefaultSizedGizmos()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));

        // A NaN multiplier must not corrupt the line list: it falls back to the default scale, so
        // the axes are still emitted at their default 0.5 length with finite vertices.
        var lines = Build(world, entity, new GizmoStyle { SizeMultiplier = float.NaN });

        Assert.Equal(3, lines.Count);
        Assert.All(
            lines,
            l =>
            {
                Assert.True(float.IsFinite(l.End.X) && float.IsFinite(l.End.Y));
                Assert.True(float.IsFinite(l.End.Z));
            }
        );
        var maxAxisReach = MathF.Max(MathF.Max(lines[0].End.X, lines[1].End.Y), lines[2].End.Z);
        Assert.Equal(0.5f, maxAxisReach, 3);
    }

    [Fact]
    public void Style_DefaultsReproduceOriginalColors()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Transform2D(new Vector2(2, 3), 0f, new Vector2(4, 2)));

        // A null style must match an explicitly default-constructed one (today's look unchanged).
        var withNull = Build(world, entity);
        var withDefault = Build(world, entity, new GizmoStyle());

        Assert.Equal(withDefault.Count, withNull.Count);
        for (var i = 0; i < withNull.Count; i++)
        {
            Assert.Equal(withDefault[i].Start, withNull[i].Start);
            Assert.Equal(withDefault[i].End, withNull[i].End);
            Assert.Equal(withDefault[i].Color, withNull[i].Color);
        }
        // And the amber bounds colour is still the original.
        Assert.Contains(withNull, l => l.Color == new Vector4(1f, 0.75f, 0.2f, 1f));
    }

    [Fact]
    public void Camera3D_NonFiniteFov_FallsBackToFiniteFrustum()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(
            entity,
            new Camera3D(
                new Vector3(0, 0, 5),
                Vector3.Zero,
                Vector3.UnitY,
                float.PositiveInfinity, // Fov
                0.1f,
                100f
            )
        );

        var lines = Build(world, entity);

        // Still a complete frustum, and no NaN/Inf leaked into the vertices.
        Assert.Equal(12, lines.Count);
        Assert.All(
            lines,
            l =>
            {
                Assert.True(
                    float.IsFinite(l.Start.X)
                        && float.IsFinite(l.Start.Y)
                        && float.IsFinite(l.Start.Z)
                );
                Assert.True(
                    float.IsFinite(l.End.X) && float.IsFinite(l.End.Y) && float.IsFinite(l.End.Z)
                );
            }
        );
    }
}
