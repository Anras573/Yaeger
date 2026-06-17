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
}
