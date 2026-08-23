using System.Numerics;
using Xunit;
using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class SkeletonRegistryTests
{
    private static Skeleton SingleBone() =>
        new([new Bone("root", -1, Matrix4x4.Identity)], [Matrix4x4.Identity]);

    [Fact]
    public void Register_ShouldReturnRetrievableHandle()
    {
        var registry = new SkeletonRegistry();
        var skeleton = SingleBone();

        var handle = registry.Register(skeleton);

        Assert.True(registry.TryGet(handle, out var stored));
        Assert.Same(skeleton, stored);
    }

    [Fact]
    public void DefaultHandle_ShouldBeInvalid()
    {
        var registry = new SkeletonRegistry();
        registry.Register(SingleBone());

        Assert.False(registry.TryGet(default, out _));
    }

    [Fact]
    public void TryGetClip_ShouldReturnRegisteredClip()
    {
        var registry = new SkeletonRegistry();
        var clip = new AnimationClip("walk", 1f, []);

        var handle = registry.Register(SingleBone(), [clip]);

        Assert.True(registry.TryGetClip(handle, "walk", out var stored));
        Assert.Same(clip, stored);
        Assert.False(registry.TryGetClip(handle, "missing", out _));
    }

    [Fact]
    public void GetClipNames_ShouldListAllClips()
    {
        var registry = new SkeletonRegistry();
        var handle = registry.Register(
            SingleBone(),
            [new AnimationClip("walk", 1f, []), new AnimationClip("run", 1f, [])]
        );

        var names = registry.GetClipNames(handle);

        Assert.Equal(2, names.Count);
        Assert.Contains("walk", names);
        Assert.Contains("run", names);
    }

    [Fact]
    public void Register_NullSkeleton_ShouldThrow()
    {
        var registry = new SkeletonRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    // ── Bone bounds (issue #197) ─────────────────────────────────────────────

    [Fact]
    public void Register_WithoutVertices_ShouldNotExposeBoneBounds()
    {
        var registry = new SkeletonRegistry();
        var handle = registry.Register(SingleBone());

        Assert.False(registry.TryGetBoneBounds(handle, out _));
    }

    [Fact]
    public void Register_WithVertices_ShouldComputeBindPositionAndMaxRadiusPerBone()
    {
        // Two independent bones (both roots, no hierarchy needed for this): bone 0's inverse bind
        // pose places its bind-pose pivot at (1,0,0); bone 1's at (-2,0,0).
        var skeleton = new Skeleton(
            [new Bone("a", -1, Matrix4x4.Identity), new Bone("b", -1, Matrix4x4.Identity)],
            [
                Matrix4x4.CreateTranslation(-1, 0, 0), // inverse of translation(1,0,0)
                Matrix4x4.CreateTranslation(2, 0, 0), // inverse of translation(-2,0,0)
            ]
        );

        var vertices = new[]
        {
            // Weighted fully to bone 0, 3 units from its pivot (1,0,0) -> (4,0,0).
            new SkinnedVertex(
                new Vector3(4, 0, 0),
                new Vector4(0, 0, 0, 0),
                new Vector4(1, 0, 0, 0)
            ),
            // Weighted fully to bone 0, only 1 unit from its pivot -> must not win over the 3-unit one.
            new SkinnedVertex(
                new Vector3(2, 0, 0),
                new Vector4(0, 0, 0, 0),
                new Vector4(1, 0, 0, 0)
            ),
            // Weighted fully to bone 1, 0.5 units from its pivot (-2,0,0).
            new SkinnedVertex(
                new Vector3(-2, 0.5f, 0),
                new Vector4(1, 0, 0, 0),
                new Vector4(1, 0, 0, 0)
            ),
        };

        var registry = new SkeletonRegistry();
        var handle = registry.Register(skeleton, vertices: vertices);

        Assert.True(registry.TryGetBoneBounds(handle, out var bounds));
        Assert.Equal(new Vector3(1, 0, 0), bounds!.BindPositions[0]);
        Assert.Equal(new Vector3(-2, 0, 0), bounds.BindPositions[1]);
        Assert.Equal(3f, bounds.Radii[0], 4);
        Assert.Equal(0.5f, bounds.Radii[1], 4);
    }

    [Fact]
    public void Register_VertexWithZeroWeightInfluence_ShouldNotContributeToRadius()
    {
        var skeleton = SingleBone();
        var vertices = new[]
        {
            // Far from the bone but with zero weight — must be ignored entirely.
            new SkinnedVertex(new Vector3(100, 0, 0), Vector4.Zero, Vector4.Zero),
        };

        var registry = new SkeletonRegistry();
        var handle = registry.Register(skeleton, vertices: vertices);

        Assert.True(registry.TryGetBoneBounds(handle, out var bounds));
        Assert.Equal(0f, bounds!.Radii[0]);
    }

    [Fact]
    public void Register_VertexWithOutOfRangeBoneIndex_ShouldBeIgnored()
    {
        var skeleton = SingleBone();
        var vertices = new[]
        {
            new SkinnedVertex(
                new Vector3(5, 0, 0),
                new Vector4(7, 0, 0, 0), // index 7 doesn't exist on a 1-bone skeleton
                new Vector4(1, 0, 0, 0)
            ),
        };

        var registry = new SkeletonRegistry();
        var handle = registry.Register(skeleton, vertices: vertices);

        Assert.True(registry.TryGetBoneBounds(handle, out var bounds));
        Assert.Equal(0f, bounds!.Radii[0]);
    }

    [Fact]
    public void Register_EmptyVertexList_ShouldExposeZeroRadiiRatherThanNoBounds()
    {
        var registry = new SkeletonRegistry();
        var handle = registry.Register(SingleBone(), vertices: []);

        Assert.True(registry.TryGetBoneBounds(handle, out var bounds));
        Assert.Equal(0f, bounds!.Radii[0]);
    }
}
