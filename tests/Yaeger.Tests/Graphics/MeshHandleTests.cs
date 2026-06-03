using Yaeger.Graphics;

namespace Yaeger.Tests.Graphics;

public class MeshHandleTests
{
    [Fact]
    public void IsStruct_SatisfiesEcsConstraint()
    {
        Assert.True(typeof(MeshHandle).IsValueType);
    }

    [Fact]
    public void Constructor_SetsId()
    {
        var handle = new MeshHandle(42);

        Assert.Equal(42, handle.Id);
    }

    [Fact]
    public void Default_HasIdZero()
    {
        var handle = default(MeshHandle);

        Assert.Equal(0, handle.Id);
    }

    [Fact]
    public void Equality_SameId_AreEqual()
    {
        var a = new MeshHandle(7);
        var b = new MeshHandle(7);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var a = new MeshHandle(1);
        var b = new MeshHandle(2);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void With_ProducesNewHandleWithUpdatedId()
    {
        var original = new MeshHandle(3);

        var updated = original with { Id = 99 };

        Assert.Equal(99, updated.Id);
        Assert.Equal(3, original.Id);
    }
}
