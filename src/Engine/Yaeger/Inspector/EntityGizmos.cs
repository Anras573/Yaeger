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
    private const float AxisLength = 0.5f;
    private const float DirectionalArrowLength = 1.5f;
    private const float DirectionalSunRadius = 0.15f;
    private const float ArrowHeadSize = 0.18f;

    // Selected meshes are outlined in amber so they stand out against typical scene colours.
    private static readonly Vector4 BoundsColor = new(1f, 0.75f, 0.2f, 1f);
    private static readonly Vector4 CameraColor = new(1f, 1f, 0.3f, 1f);

    /// <summary>
    /// Appends the gizmos for <paramref name="entity"/> to <paramref name="builder"/> based on the
    /// components it carries. <paramref name="aspectRatio"/> is only used to shape a
    /// <see cref="Camera3D"/> frustum.
    /// </summary>
    public static void Build(World world, Entity entity, float aspectRatio, GizmoBuilder builder)
    {
        var hasTransform = world.TryGetComponent<Transform3D>(entity, out var transform);
        var anchor = hasTransform ? transform.Position : Vector3.Zero;

        // Orientation axes + bounds give "where is the selected entity" for any 3D entity, meshes
        // especially. Drawn first so light/camera specific shapes layer on top.
        if (hasTransform)
        {
            builder.AddAxes(anchor, transform.Rotation, AxisLength);

            if (world.TryGetComponent<Aabb3D>(entity, out var aabb))
                builder.AddTransformedBox(aabb, transform.ModelMatrix, BoundsColor);
        }

        if (world.TryGetComponent<DirectionalLight>(entity, out var directional))
            BuildDirectionalLight(builder, anchor, directional);

        if (world.TryGetComponent<PointLight>(entity, out var point))
            BuildPointLight(builder, anchor, point);

        if (world.TryGetComponent<SpotLight>(entity, out var spot))
            BuildSpotLight(builder, anchor, spot);

        if (world.TryGetComponent<Camera3D>(entity, out var camera))
            BuildCamera(builder, camera, aspectRatio);
    }

    private static void BuildDirectionalLight(
        GizmoBuilder builder,
        Vector3 anchor,
        DirectionalLight light
    )
    {
        var color = OpaqueColor(light.Color);

        // Direction points from the surface toward the source, so light *travels* the opposite way.
        // Visualise the travel direction as a bundle of parallel "sun ray" arrows — the standard
        // editor cue for a directional light's facing.
        var travel = SafeNormalize(-light.Direction, -Vector3.UnitY);
        ParallelBasis(travel, out var u, out var v);

        builder.AddWireSphere(anchor, DirectionalSunRadius, color, segments: 16);

        ReadOnlySpan<Vector2> offsets =
        [
            Vector2.Zero,
            new(1f, 0f),
            new(-1f, 0f),
            new(0f, 1f),
            new(0f, -1f),
        ];

        const float spread = 0.18f;
        foreach (var offset in offsets)
        {
            var shift = (u * offset.X + v * offset.Y) * spread;
            var start = anchor + shift;
            builder.AddArrow(start, start + travel * DirectionalArrowLength, color, ArrowHeadSize);
        }
    }

    private static void BuildPointLight(GizmoBuilder builder, Vector3 anchor, PointLight light)
    {
        var color = OpaqueColor(light.Color);
        var range = light.Range > 0f ? light.Range : 1f;

        // The wire sphere traces the range (where the contribution falls to zero); a small core
        // marks the exact position even when the range sphere is large.
        builder.AddWireSphere(anchor, range, color);
        builder.AddWireSphere(anchor, MathF.Min(0.1f, range * 0.1f), color, segments: 12);
    }

    private static void BuildSpotLight(GizmoBuilder builder, Vector3 anchor, SpotLight light)
    {
        var color = OpaqueColor(light.Color);
        var range = light.Range > 0f ? light.Range : 1f;
        var direction = SafeNormalize(light.Direction, -Vector3.UnitY);

        // Base radius from the outer cone half-angle, clamped just below 90° so tan stays finite.
        var outer = Math.Clamp(light.OuterConeAngle, 0f, 1.55f);
        var baseRadius = range * MathF.Tan(outer);

        builder.AddWireCone(anchor, direction, range, baseRadius, color);
    }

    private static void BuildCamera(GizmoBuilder builder, Camera3D camera, float aspectRatio)
    {
        var forward = SafeNormalize(camera.Target - camera.Position, -Vector3.UnitZ);
        var up = SafeNormalize(camera.Up, Vector3.UnitY);
        var right = SafeNormalize(Vector3.Cross(forward, up), Vector3.UnitX);
        up = Vector3.Cross(right, forward);

        var aspect = aspectRatio > 0f && float.IsFinite(aspectRatio) ? aspectRatio : 16f / 9f;
        var fov = camera.Fov > 0f ? camera.Fov : MathF.PI / 4f;
        var near = camera.Near > 0f ? camera.Near : 0.1f;

        // Cap the visualised far plane so a frustum drawn for a 1000-unit camera stays on screen.
        var far = MathF.Min(camera.Far > near ? camera.Far : near + 1f, near + 3f);

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
            builder.AddLine(nearCorners[i], nearCorners[next], CameraColor);
            builder.AddLine(farCorners[i], farCorners[next], CameraColor);
            builder.AddLine(nearCorners[i], farCorners[i], CameraColor);
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
