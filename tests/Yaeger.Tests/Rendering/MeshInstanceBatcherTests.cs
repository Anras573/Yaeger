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

    [Fact]
    public void Add_NonSkinnedGroup_IsNotSkinned()
    {
        var batcher = new MeshInstanceBatcher();
        batcher.Add(new MeshHandle(1), MaterialA, Matrix4x4.Identity);

        var group = batcher.Groups.Single();

        Assert.False(group.IsSkinned);
        Assert.Equal(default, group.Skeleton);
        Assert.Empty(group.BonePalettes);
        Assert.Empty(group.PaletteOffsets);
    }

    [Fact]
    public void AddSkinned_SameMeshMaterialAndSkeleton_AccumulatesIntoOneGroup()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        var skeleton = new SkeletonHandle(1);

        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[2]);
        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[2]);

        var group = batcher.Groups.Single();

        Assert.True(group.IsSkinned);
        Assert.Equal(skeleton, group.Skeleton);
        Assert.Equal(2, group.Models.Count);
        Assert.Equal(2, group.BonePalettes.Count);
    }

    [Fact]
    public void AddSkinned_DifferentSkeleton_ProducesSeparateGroupsEvenWithSameMeshAndMaterial()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);

        batcher.AddSkinned(
            handle,
            MaterialA,
            new SkeletonHandle(1),
            Matrix4x4.Identity,
            new Matrix4x4[2]
        );
        batcher.AddSkinned(
            handle,
            MaterialA,
            new SkeletonHandle(2),
            Matrix4x4.Identity,
            new Matrix4x4[3]
        );

        var groups = batcher.Groups.ToList();

        Assert.Equal(2, groups.Count);
        Assert.All(groups, g => Assert.Single(g.Models));
    }

    [Fact]
    public void AddSkinned_ComputesCumulativePaletteOffsetsFromEachInstancesBoneCount()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        var skeleton = new SkeletonHandle(1);

        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[2]);
        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[3]);
        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[4]);

        var group = batcher.Groups.Single();

        Assert.Equal([0, 2, 5], group.PaletteOffsets);
    }

    [Fact]
    public void Clear_ThenAddSkinned_ResetsCumulativePaletteOffset()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);
        var skeleton = new SkeletonHandle(1);
        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[5]);

        batcher.Clear();
        batcher.AddSkinned(handle, MaterialA, skeleton, Matrix4x4.Identity, new Matrix4x4[3]);

        var group = batcher.Groups.Single();

        Assert.Equal([0], group.PaletteOffsets);
    }

    [Fact]
    public void Add_AndAddSkinned_SameMeshAndMaterial_ProduceSeparateGroups()
    {
        var batcher = new MeshInstanceBatcher();
        var handle = new MeshHandle(1);

        batcher.Add(handle, MaterialA, Matrix4x4.Identity);
        batcher.AddSkinned(
            handle,
            MaterialA,
            new SkeletonHandle(1),
            Matrix4x4.Identity,
            new Matrix4x4[2]
        );

        var groups = batcher.Groups.ToList();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => !g.IsSkinned);
        Assert.Contains(groups, g => g.IsSkinned);
    }

    [Fact]
    public void AddSkinned_NullBonePalette_Throws()
    {
        var batcher = new MeshInstanceBatcher();

        Assert.Throws<ArgumentNullException>(() =>
            batcher.AddSkinned(
                new MeshHandle(1),
                MaterialA,
                new SkeletonHandle(1),
                Matrix4x4.Identity,
                null!
            )
        );
    }
}
