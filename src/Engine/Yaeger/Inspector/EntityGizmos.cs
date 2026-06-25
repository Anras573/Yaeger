using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

/// <summary>
/// Translates the components on a selected entity into editor gizmos — the world-space overlay that
/// lets you <em>see</em> what you are editing: where a light sits, which way it faces, how far it
/// reaches, where a mesh's bounds are, and what a camera frames. Pure (no GPU state): it only reads
/// the world and appends lines to a <see cref="GizmoBuilder"/>, so it is fully unit-testable.
/// </summary>
public static class EntityGizmos
{
    /// <summary>
    /// Appends the gizmos for <paramref name="entity"/> to <paramref name="builder"/> based on the
    /// components it carries. <paramref name="aspectRatio"/> shapes a <see cref="Camera3D"/> frustum
    /// and a <see cref="Camera2D"/> viewport rectangle. <paramref name="style"/> tunes colours, sizes
    /// and segment counts; when <c>null</c> a default style reproducing the original look is used.
    /// </summary>
    public static void Build(
        World world,
        Entity entity,
        float aspectRatio,
        GizmoBuilder builder,
        GizmoStyle? style = null
    )
    {
        style ??= new GizmoStyle();
        var scale = style.SizeMultiplier;
        // 2D gizmos live in the Z = 0 plane and are projected through a Camera2D-derived (or
        // identity) view-projection by the inspector. An entity is treated as either 2D or 3D for
        // gizmo purposes — never both — to stay consistent with
        // ImGuiInspector.TryGetSceneViewProjection, which switches to that 2D view whenever a
        // Transform2D/Camera2D is present. Emitting 3D gizmos too would draw them with the wrong
        // (2D) projection for an entity that happens to carry both (the Add Component menu allows
        // it), so once any 2D gizmo is emitted we skip the 3D path entirely.
        var has2D = false;

        if (world.TryGetComponent<Transform2D>(entity, out var transform2D))
        {
            BuildTransform2D(builder, transform2D, style, scale);
            has2D = true;
        }

        if (world.TryGetComponent<Camera2D>(entity, out var camera2D))
        {
            BuildCamera2D(builder, camera2D, aspectRatio, style);
            has2D = true;
        }

        if (has2D)
            return;

        var hasTransform = world.TryGetComponent<Transform3D>(entity, out var transform);
        var anchor = hasTransform ? transform.Position : Vector3.Zero;

        // Orientation axes + bounds give "where is the selected entity" for any 3D entity, meshes
        // especially. Drawn first so light/camera specific shapes layer on top.
        if (hasTransform)
        {
            builder.AddAxes(
                anchor,
                transform.Rotation,
                style.AxisLength * scale,
                style.AxisXColor,
                style.AxisYColor,
                style.AxisZColor
            );

            if (world.TryGetComponent<Aabb3D>(entity, out var aabb))
                builder.AddTransformedBox(aabb, transform.ModelMatrix, style.BoundsColor);
        }

        if (world.TryGetComponent<DirectionalLight>(entity, out var directional))
            BuildDirectionalLight(builder, anchor, directional, style, scale);

        if (world.TryGetComponent<PointLight>(entity, out var point))
            BuildPointLight(builder, anchor, point, style, scale);

        if (world.TryGetComponent<SpotLight>(entity, out var spot))
            BuildSpotLight(builder, anchor, spot, style);

        if (world.TryGetComponent<Camera3D>(entity, out var camera))
            BuildCamera(builder, camera, aspectRatio, style);
    }

    private static void BuildTransform2D(
        GizmoBuilder builder,
        Transform2D transform,
        GizmoStyle style,
        float scale
    )
    {
        // Oriented X/Y axes mark the origin and facing; the bounds rectangle traces the sprite quad
        // (the renderer draws a unit quad scaled by Transform2D.Scale), so it lines up with what is
        // actually rendered.
        builder.AddAxes2D(
            transform.Position,
            transform.Rotation,
            style.Axis2DLength * scale,
            style.AxisXColor,
            style.AxisYColor
        );
        builder.AddRect(
            transform.Position,
            transform.Scale * 0.5f,
            transform.Rotation,
            style.BoundsColor
        );
    }

    private static void BuildCamera2D(
        GizmoBuilder builder,
        Camera2D camera,
        float aspectRatio,
        GizmoStyle style
    )
    {
        // Mirror Camera2D.ViewProjection's zoom guard exactly (Zoom > 0 ? Zoom : 1) so the drawn
        // rectangle agrees with what the camera actually frames. A +Infinity zoom is kept as-is,
        // matching the renderer: aspect/∞ and 1/∞ both collapse to 0, so the viewport shrinks to a
        // point while staying finite. Aspect is separately guarded against a non-finite value.
        var aspect = aspectRatio > 0f && float.IsFinite(aspectRatio) ? aspectRatio : 16f / 9f;
        var zoom = camera.Zoom > 0f ? camera.Zoom : 1f;

        var halfExtents = new Vector2(aspect / zoom, 1f / zoom);
        builder.AddRect(camera.Position, halfExtents, camera.Rotation, style.CameraColor);
    }

    private static void BuildDirectionalLight(
        GizmoBuilder builder,
        Vector3 anchor,
        DirectionalLight light,
        GizmoStyle style,
        float scale
    )
    {
        var color = style.DirectionalLightColor ?? OpaqueColor(light.Color);

        // Direction points from the surface toward the source, so light *travels* the opposite way.
        // Visualise the travel direction as a bundle of parallel "sun ray" arrows — the standard
        // editor cue for a directional light's facing.
        var travel = SafeNormalize(-light.Direction, -Vector3.UnitY);
        ParallelBasis(travel, out var u, out var v);

        builder.AddWireSphere(
            anchor,
            style.DirectionalSunRadius * scale,
            color,
            style.SunSphereSegments
        );

        ReadOnlySpan<Vector2> offsets =
        [
            Vector2.Zero,
            new(1f, 0f),
            new(-1f, 0f),
            new(0f, 1f),
            new(0f, -1f),
        ];

        var spread = style.DirectionalRaySpread * scale;
        var arrowLength = style.DirectionalArrowLength * scale;
        var headSize = style.ArrowHeadSize * scale;
        foreach (var offset in offsets)
        {
            var shift = (u * offset.X + v * offset.Y) * spread;
            var start = anchor + shift;
            builder.AddArrow(start, start + travel * arrowLength, color, headSize);
        }
    }

    private static void BuildPointLight(
        GizmoBuilder builder,
        Vector3 anchor,
        PointLight light,
        GizmoStyle style,
        float scale
    )
    {
        var color = style.PointLightColor ?? OpaqueColor(light.Color);
        var coreSize = style.PointLightCoreSize * scale;

        // A non-positive (or non-finite) range disables the light in the renderer (attenuate
        // returns 0), so don't draw a misleading reach sphere for it. The small core is always
        // drawn so the light's position stays visible even when disabled.
        if (float.IsFinite(light.Range) && light.Range > 0f)
        {
            builder.AddWireSphere(anchor, light.Range, color, style.SphereSegments);
            builder.AddWireSphere(
                anchor,
                MathF.Min(coreSize, light.Range * 0.1f),
                color,
                style.PointLightCoreSegments
            );
        }
        else
        {
            builder.AddWireSphere(anchor, coreSize, color, style.PointLightCoreSegments);
        }
    }

    private static void BuildSpotLight(
        GizmoBuilder builder,
        Vector3 anchor,
        SpotLight light,
        GizmoStyle style
    )
    {
        // A non-positive (or non-finite) range disables the light, so skip the cone entirely — the
        // Transform3D axes still mark the light's position and orientation.
        if (!(float.IsFinite(light.Range) && light.Range > 0f))
            return;

        var color = style.SpotLightColor ?? OpaqueColor(light.Color);
        var direction = SafeNormalize(light.Direction, -Vector3.UnitY);

        // Coerce a non-finite outer angle to a safe default before clamping so a NaN/Inf set by a
        // script can't propagate through tan() into the generated vertices. Clamp just below 90° so
        // tan stays finite.
        var outerRaw = float.IsFinite(light.OuterConeAngle) ? light.OuterConeAngle : MathF.PI / 6f;
        var outer = Math.Clamp(outerRaw, 0f, 1.55f);
        var baseRadius = light.Range * MathF.Tan(outer);

        builder.AddWireCone(anchor, direction, light.Range, baseRadius, color, style.ConeSegments);
    }

    private static void BuildCamera(
        GizmoBuilder builder,
        Camera3D camera,
        float aspectRatio,
        GizmoStyle style
    )
    {
        // A non-finite position/target/up would survive SafeNormalize (which falls back) but still
        // poison the frustum corners via camera.Position, so bail before computing anything.
        if (!IsFinite(camera.Position) || !IsFinite(camera.Target) || !IsFinite(camera.Up))
            return;

        var forward = SafeNormalize(camera.Target - camera.Position, -Vector3.UnitZ);
        var up = SafeNormalize(camera.Up, Vector3.UnitY);
        var right = SafeNormalize(Vector3.Cross(forward, up), Vector3.UnitX);
        up = Vector3.Cross(right, forward);

        // Require finiteness too, not just > 0: a +Inf Fov/Near would flow into tan()/the corners
        // and emit NaN vertices, matching the NaN/Inf guarding used elsewhere in this file.
        var aspect = aspectRatio > 0f && float.IsFinite(aspectRatio) ? aspectRatio : 16f / 9f;
        var fov = camera.Fov > 0f && float.IsFinite(camera.Fov) ? camera.Fov : MathF.PI / 4f;
        var near = camera.Near > 0f && float.IsFinite(camera.Near) ? camera.Near : 0.1f;

        // Cap the visualised far plane so a frustum drawn for a 1000-unit camera stays on screen.
        var farRaw = camera.Far > near && float.IsFinite(camera.Far) ? camera.Far : near + 1f;
        var far = MathF.Min(farRaw, near + 3f);

        var tanHalf = MathF.Tan(fov * 0.5f);
        var nearH = tanHalf * near;
        var nearW = nearH * aspect;
        var farH = tanHalf * far;
        var farW = farH * aspect;

        var nearCenter = camera.Position + forward * near;
        var farCenter = camera.Position + forward * far;

        var nearCorners = RectCorners(nearCenter, right, up, nearW, nearH);
        var farCorners = RectCorners(farCenter, right, up, farW, farH);

        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            builder.AddLine(nearCorners[i], nearCorners[next], style.CameraColor);
            builder.AddLine(farCorners[i], farCorners[next], style.CameraColor);
            builder.AddLine(nearCorners[i], farCorners[i], style.CameraColor);
        }
    }

    private static Vector3[] RectCorners(
        Vector3 center,
        Vector3 right,
        Vector3 up,
        float halfWidth,
        float halfHeight
    ) =>
        [
            center + right * halfWidth + up * halfHeight,
            center + right * halfWidth - up * halfHeight,
            center - right * halfWidth - up * halfHeight,
            center - right * halfWidth + up * halfHeight,
        ];

    private static Vector4 OpaqueColor(Color color)
    {
        var v = color.ToVector4();
        return new Vector4(v.X, v.Y, v.Z, 1f);
    }

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        var lengthSq = value.LengthSquared();
        return float.IsFinite(lengthSq) && lengthSq > 1e-12f
            ? value / MathF.Sqrt(lengthSq)
            : fallback;
    }

    // Orthonormal basis perpendicular to a unit direction, used to spread the directional rays.
    private static void ParallelBasis(Vector3 dir, out Vector3 u, out Vector3 v)
    {
        var reference = MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        u = Vector3.Normalize(Vector3.Cross(reference, dir));
        v = Vector3.Cross(dir, u);
    }
}
