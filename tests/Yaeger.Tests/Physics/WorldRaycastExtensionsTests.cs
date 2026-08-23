using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics;

namespace Yaeger.Tests.Physics;

public class WorldRaycastExtensionsTests
{
    private static Entity SpawnBox(World world, Vector3 position, Aabb3D localBounds)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, Transform3D.Identity with { Position = position });
        world.AddComponent(entity, localBounds);
        return entity;
    }

    // ── Raycast: hit and miss ────────────────────────────────────────────────

    [Fact]
    public void Raycast_RayHitsBox_ReturnsHit()
    {
        var world = new World();
        var box = SpawnBox(world, Vector3.Zero, new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(new Vector3(0, 0, 5), new Vector3(0, 0, -1));

        var hit = world.Raycast(ray);

        Assert.NotNull(hit);
        Assert.Equal(box, hit!.Value.Entity);
        Assert.Equal(4f, hit.Value.Distance, 3);
        Assert.Equal(new Vector3(0, 0, 1), hit.Value.Point);
    }

    [Fact]
    public void Raycast_RayMissesEveryBox_ReturnsNull()
    {
        var world = new World();
        SpawnBox(world, Vector3.Zero, new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(new Vector3(10, 0, 5), new Vector3(0, 0, -1));

        Assert.Null(world.Raycast(ray));
    }

    // ── Nearest-first ordering ───────────────────────────────────────────────

    [Fact]
    public void Raycast_SeveralCandidates_ReturnsNearest()
    {
        var world = new World();
        var far = SpawnBox(
            world,
            new Vector3(0, 0, -20),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var near = SpawnBox(
            world,
            new Vector3(0, 0, -5),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var mid = SpawnBox(
            world,
            new Vector3(0, 0, -10),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var ray = new Ray3D(new Vector3(0, 0, 10), new Vector3(0, 0, -1));

        var hit = world.Raycast(ray);

        Assert.NotNull(hit);
        Assert.Equal(near, hit!.Value.Entity);
        Assert.NotEqual(mid, hit.Value.Entity);
        Assert.NotEqual(far, hit.Value.Entity);
    }

    [Fact]
    public void RaycastAll_SeveralCandidates_ReturnsSortedNearestFirst()
    {
        var world = new World();
        var far = SpawnBox(
            world,
            new Vector3(0, 0, -20),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var near = SpawnBox(
            world,
            new Vector3(0, 0, -5),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var mid = SpawnBox(
            world,
            new Vector3(0, 0, -10),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var ray = new Ray3D(new Vector3(0, 0, 10), new Vector3(0, 0, -1));

        var hits = world.RaycastAll(ray);

        Assert.Equal(3, hits.Count);
        Assert.Equal(near, hits[0].Entity);
        Assert.Equal(mid, hits[1].Entity);
        Assert.Equal(far, hits[2].Entity);
        Assert.True(hits[0].Distance < hits[1].Distance);
        Assert.True(hits[1].Distance < hits[2].Distance);
    }

    [Fact]
    public void RaycastAll_NoCandidates_ReturnsEmpty()
    {
        var world = new World();
        var ray = new Ray3D(Vector3.Zero, Vector3.UnitZ);

        Assert.Empty(world.RaycastAll(ray));
    }

    // ── Ray originating inside a box ────────────────────────────────────────

    [Fact]
    public void Raycast_RayOriginatesInsideBox_ReturnsExitPoint()
    {
        var world = new World();
        var box = SpawnBox(world, Vector3.Zero, new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, -1));

        var hit = world.Raycast(ray);

        Assert.NotNull(hit);
        Assert.Equal(box, hit!.Value.Entity);
        Assert.Equal(1f, hit.Value.Distance, 3);
        Assert.Equal(new Vector3(0, 0, -1), hit.Value.Point);
    }

    // ── Ray parallel to a slab ───────────────────────────────────────────────

    [Fact]
    public void Raycast_RayParallelToSlabButPassesThroughOtherAxes_ReturnsHit()
    {
        // Ray travels only along Z, at X = 0 which lies within the box's X slab — parallel to
        // the X (and Y) slabs but still passes through the box via the Z axis.
        var world = new World();
        var box = SpawnBox(world, Vector3.Zero, new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(new Vector3(0, 0, 5), new Vector3(0, 0, -1));

        Assert.NotNull(world.Raycast(ray));

        // Same ray, but offset outside the X slab: parallel and outside means a miss.
        var missRay = new Ray3D(new Vector3(5, 0, 5), new Vector3(0, 0, -1));
        Assert.Null(world.Raycast(missRay));
    }

    // ── Zero-thickness bounds ───────────────────────────────────────────────

    [Fact]
    public void Raycast_ZeroThicknessBoundsAlongRayAxis_ReturnsHit()
    {
        // A flat box (zero thickness on Z) acting as a plane; the ray travels straight through it.
        var world = new World();
        var box = SpawnBox(
            world,
            Vector3.Zero,
            new Aabb3D(new Vector3(-1, -1, 0), new Vector3(1, 1, 0))
        );
        var ray = new Ray3D(new Vector3(0, 0, 5), new Vector3(0, 0, -1));

        var hit = world.Raycast(ray);

        Assert.NotNull(hit);
        Assert.Equal(box, hit!.Value.Entity);
        Assert.Equal(5f, hit.Value.Distance, 3);
    }

    // ── maxDistance ──────────────────────────────────────────────────────────

    [Fact]
    public void Raycast_HitBeyondMaxDistance_ReturnsNull()
    {
        var world = new World();
        SpawnBox(world, new Vector3(0, 0, -10), new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, -1));

        Assert.Null(world.Raycast(ray, maxDistance: 5f));
    }

    [Fact]
    public void Raycast_HitWithinMaxDistance_ReturnsHit()
    {
        var world = new World();
        var box = SpawnBox(
            world,
            new Vector3(0, 0, -10),
            new Aabb3D(new Vector3(-1), new Vector3(1))
        );
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, -1));

        var hit = world.Raycast(ray, maxDistance: 20f);

        Assert.NotNull(hit);
        Assert.Equal(box, hit!.Value.Entity);
    }

    // ── Mask filtering ───────────────────────────────────────────────────────

    [Fact]
    public void Raycast_MaskExcludesEntityLayer_SkipsThatEntity()
    {
        var world = new World();
        var excluded = SpawnBox(
            world,
            new Vector3(0, 0, -5),
            new Aabb3D(new Vector3(-1), new Vector3(1)) { Layer = 3 }
        );
        var included = SpawnBox(
            world,
            new Vector3(0, 0, -10),
            new Aabb3D(new Vector3(-1), new Vector3(1)) { Layer = 1 }
        );
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, -1));

        var hit = world.Raycast(ray, mask: 1u << 1);

        Assert.NotNull(hit);
        Assert.Equal(included, hit!.Value.Entity);
        Assert.NotEqual(excluded, hit.Value.Entity);
    }

    [Fact]
    public void RaycastAll_MaskExcludesLayer_OmitsThoseHits()
    {
        var world = new World();
        SpawnBox(
            world,
            new Vector3(0, 0, -5),
            new Aabb3D(new Vector3(-1), new Vector3(1)) { Layer = 3 }
        );
        var included = SpawnBox(
            world,
            new Vector3(0, 0, -10),
            new Aabb3D(new Vector3(-1), new Vector3(1)) { Layer = 1 }
        );
        var ray = new Ray3D(Vector3.Zero, new Vector3(0, 0, -1));

        var hits = world.RaycastAll(ray, mask: 1u << 1);

        Assert.Single(hits);
        Assert.Equal(included, hits[0].Entity);
    }

    [Fact]
    public void Raycast_DefaultMask_HitsUnconfiguredLayer()
    {
        // Layer defaults to 0, and the default mask (AllLayers) includes every bit.
        var world = new World();
        var box = SpawnBox(world, Vector3.Zero, new Aabb3D(new Vector3(-1), new Vector3(1)));
        var ray = new Ray3D(new Vector3(0, 0, 5), new Vector3(0, 0, -1));

        Assert.Equal(box, world.Raycast(ray)!.Value.Entity);
    }
}
