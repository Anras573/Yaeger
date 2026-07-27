using System.Numerics;
using Yaeger.Graphics;
using Yaeger.Rendering;

namespace Yaeger.Tests.Rendering;

// Pure CPU-side grouping logic — no GL context needed, unlike the rest of Rendering/.
public class MeshInstanceBatcherTests
{
    private static readonly Material3D MaterialA = new() { Diffuse = Color.Red };
    private static readonly Material3D MaterialB = new() { Diffuse = Color.Blue };

    [Fact]
    public void Add_SameMeshAndMaterial_AccumulatesIntoOneGroup()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);

        batcher.Add(handle, MaterialA, Matrix4x4.CreateTranslation(1f, 0f, 0f));
        batcher.Add(handle, MaterialA, Matrix4x4.CreateTranslation(2f, 0f, 0f));
        batcher.Add(handle, MaterialA, Matrix4x4.CreateTranslation(3f, 0f, 0f));

        var groups = batcher.Groups.ToList();

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Models.Count);
    }

    [Fact]
    public void Add_DifferentMeshHandle_ProducesSeparateGroups()
    {
        var batcher = new MeshInstanceBatcher();

        batcher.Add(new MeshHandle(1), MaterialA, Matrix4x4.Identity);
        batcher.Add(new MeshHandle(2), MaterialA, Matrix4x4.Identity);

        var groups = batcher.Groups.ToList();

        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Single(g.Models));
    }

    [Fact]
    public void Add_DifferentMaterial_ProducesSeparateGroups()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);

        batcher.Add(handle, MaterialA, Matrix4x4.Identity);
        batcher.Add(handle, MaterialB, Matrix4x4.Identity);

        var groups = batcher.Groups.ToList();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Material.Equals(MaterialA));
        Assert.Contains(groups, g => g.Material.Equals(MaterialB));
    }

    [Fact]
    public void Add_PreservesInsertionOrderWithinGroup()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        var first = Matrix4x4.CreateTranslation(1f, 0f, 0f);
        var second = Matrix4x4.CreateTranslation(2f, 0f, 0f);
        var third = Matrix4x4.CreateTranslation(3f, 0f, 0f);

        batcher.Add(handle, MaterialA, first);
        batcher.Add(handle, MaterialA, second);
        batcher.Add(handle, MaterialA, third);

        var models = batcher.Groups.Single().Models;

        Assert.Equal([first, second, third], models);
    }

    [Fact]
    public void Groups_BeforeAnyAdd_IsEmpty()
    {
        var batcher = new MeshInstanceBatcher();

        Assert.Empty(batcher.Groups);
    }

    [Fact]
    public void Clear_EmptiesGroups()
    {
        var batcher = new MeshInstanceBatcher();
        batcher.Add(new MeshHandle(1), MaterialA, Matrix4x4.Identity);

        batcher.Clear();

        Assert.Empty(batcher.Groups);
    }

    [Fact]
    public void Clear_ThenAdd_ReusesGroupWithFreshContents()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        batcher.Add(handle, MaterialA, Matrix4x4.CreateTranslation(1f, 0f, 0f));
        batcher.Add(handle, MaterialA, Matrix4x4.CreateTranslation(2f, 0f, 0f));

        batcher.Clear();
        var next = Matrix4x4.CreateTranslation(9f, 0f, 0f);
        batcher.Add(handle, MaterialA, next);

        var groups = batcher.Groups.ToList();

        Assert.Single(groups);
        Assert.Equal([next], groups[0].Models);
    }

    [Fact]
    public void Groups_SkipsGroupsClearedToEmpty()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        batcher.Add(handle, MaterialA, Matrix4x4.Identity);

        batcher.Clear();

        // The group's list still exists internally (kept for capacity reuse) but has zero
        // entries, so it must not surface as an empty group.
        Assert.Empty(batcher.Groups);
    }
}
