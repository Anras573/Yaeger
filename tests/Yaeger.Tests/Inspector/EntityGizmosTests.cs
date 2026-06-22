using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class EntityGizmosTests
{
    private const float Aspect = 16f / 9f;

    private static IReadOnlyList<GizmoLine> Build(World world, Entity entity)
    {
        var builder = new GizmoBuilder();
        EntityGizmos.Build(world, entity, Aspect, builder);
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

        // The box edges should sit around the world position (10 ± 1 on X).
        Assert.Contains(lines, l => l.Start.X is >= 9f and <= 11f && l.Start.X > 1f);
    }

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
}
