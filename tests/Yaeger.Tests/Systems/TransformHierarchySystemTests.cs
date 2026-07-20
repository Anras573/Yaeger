using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Systems;

namespace Yaeger.Tests.Systems;

public class TransformHierarchySystemTests
{
    private const int Precision = 4;

    // ── 2D ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ParentChild_TranslationOnly_ComposesWorldPosition()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(parent, new Transform2D(new Vector2(5f, 0f)));

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(child, new LocalTransform2D(new Vector2(0f, 2f)));

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(child, out var childWorld));
        AssertClose(new Vector2(5f, 2f), childWorld.Position);
        Assert.Equal(0f, childWorld.Rotation, Precision);
        AssertClose(Vector2.One, childWorld.Scale);
    }

    [Fact]
    public void Update_ParentRotated90Degrees_RotatesChildLocalOffsetIntoWorldSpace()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(parent, new Transform2D(Vector2.Zero, MathF.PI / 2f));

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(child, new LocalTransform2D(new Vector2(1f, 0f)));

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(child, out var childWorld));
        AssertClose(new Vector2(0f, 1f), childWorld.Position);
        Assert.Equal(MathF.PI / 2f, childWorld.Rotation, Precision);
    }

    [Fact]
    public void Update_ParentScaled_MultipliesScaleComponentWise()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(parent, new Transform2D(Vector2.Zero, 0f, new Vector2(2f, 2f)));

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(child, new LocalTransform2D(Vector2.Zero, 0f, new Vector2(3f, 3f)));

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(child, out var childWorld));
        AssertClose(new Vector2(6f, 6f), childWorld.Scale);
    }

    [Fact]
    public void Update_ThreeLevelHierarchy_ComposesTransitively()
    {
        var world = new World();
        var grandparent = world.CreateEntity();
        world.AddComponent(grandparent, new Transform2D(new Vector2(10f, 0f)));

        var parent = world.CreateEntity();
        world.AddComponent(parent, new Parent(grandparent));
        world.AddComponent(parent, new LocalTransform2D(new Vector2(1f, 0f)));

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(child, new LocalTransform2D(new Vector2(1f, 0f)));

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(parent, out var parentWorld));
        AssertClose(new Vector2(11f, 0f), parentWorld.Position);

        Assert.True(world.TryGetComponent<Transform2D>(child, out var childWorld));
        AssertClose(new Vector2(12f, 0f), childWorld.Position);
    }

    [Fact]
    public void Update_RootEntityWithoutParent_IsLeftUntouched()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var original = new Transform2D(new Vector2(3f, 4f), 1.2f, new Vector2(2f, 3f));
        world.AddComponent(entity, original);

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(entity, out var afterUpdate));
        AssertClose(original.Position, afterUpdate.Position);
        Assert.Equal(original.Rotation, afterUpdate.Rotation, Precision);
        AssertClose(original.Scale, afterUpdate.Scale);
    }

    [Fact]
    public void Update_ParentWithoutLocalTransform_ChildTreatedAsRootAndUntouched()
    {
        // The "parent" entity carries Parent but no LocalTransform2D, so it does not
        // participate in composition itself and is read as a root by its own child.
        var world = new World();
        var logicalGroup = world.CreateEntity();
        var unrelated = world.CreateEntity();
        world.AddComponent(logicalGroup, new Parent(unrelated)); // no LocalTransform2D
        world.AddComponent(logicalGroup, new Transform2D(new Vector2(7f, 7f)));

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform2D>(logicalGroup, out var stillTheSame));
        AssertClose(new Vector2(7f, 7f), stillTheSame.Position);
    }

    [Fact]
    public void Update_ParentDestroyed_OrphansChildToLastWorldTransformAndRemovesParentComponent()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(parent, new Transform2D(new Vector2(5f, 0f)));

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(child, new LocalTransform2D(new Vector2(0f, 2f)));

        var system = new TransformHierarchySystem(world);
        system.Update(0f);
        Assert.True(world.TryGetComponent<Transform2D>(child, out var beforeDestroy));

        world.DestroyEntity(parent);
        system.Update(0f);

        Assert.False(world.TryGetComponent<Parent>(child, out _));
        Assert.True(world.TryGetComponent<Transform2D>(child, out var afterDestroy));
        AssertClose(beforeDestroy.Position, afterDestroy.Position);
    }

    [Fact]
    public void Update_SelfParent_ThrowsInvalidOperationException()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Parent(entity));
        world.AddComponent(entity, new LocalTransform2D(Vector2.Zero));

        Assert.Throws<InvalidOperationException>(() =>
            new TransformHierarchySystem(world).Update(0f)
        );
    }

    [Fact]
    public void Update_CyclicParentChain_ThrowsInvalidOperationException()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent(a, new LocalTransform2D(Vector2.Zero));
        world.AddComponent(b, new LocalTransform2D(Vector2.Zero));
        world.AddComponent(a, new Parent(b));
        world.AddComponent(b, new Parent(a));

        Assert.Throws<InvalidOperationException>(() =>
            new TransformHierarchySystem(world).Update(0f)
        );
    }

    // ── 3D ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Update3D_ParentChild_TranslationOnly_ComposesWorldPosition()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(
            parent,
            Transform3D.Identity with
            {
                Position = new Vector3(1f, 2f, 3f),
            }
        );

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(
            child,
            LocalTransform3D.Identity with
            {
                Position = new Vector3(4f, 5f, 6f),
            }
        );

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform3D>(child, out var childWorld));
        AssertClose(new Vector3(5f, 7f, 9f), childWorld.Position);
    }

    [Fact]
    public void Update3D_ParentScaled_MultipliesScaleComponentWise()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(parent, Transform3D.Identity with { Scale = new Vector3(2f, 2f, 2f) });

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(
            child,
            LocalTransform3D.Identity with
            {
                Scale = new Vector3(3f, 3f, 3f),
            }
        );

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform3D>(child, out var childWorld));
        AssertClose(new Vector3(6f, 6f, 6f), childWorld.Scale);
    }

    [Fact]
    public void Update3D_ParentRotated90DegreesAboutZ_RotatesChildLocalOffsetIntoWorldSpace()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(
            parent,
            Transform3D.Identity with
            {
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
            }
        );

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(
            child,
            LocalTransform3D.Identity with
            {
                Position = new Vector3(1f, 0f, 0f),
            }
        );

        new TransformHierarchySystem(world).Update(0f);

        Assert.True(world.TryGetComponent<Transform3D>(child, out var childWorld));
        AssertClose(new Vector3(0f, 1f, 0f), childWorld.Position);
    }

    [Fact]
    public void Update3D_ParentDestroyed_OrphansChildAndRemovesParentComponent()
    {
        var world = new World();
        var parent = world.CreateEntity();
        world.AddComponent(
            parent,
            Transform3D.Identity with
            {
                Position = new Vector3(5f, 0f, 0f),
            }
        );

        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));
        world.AddComponent(
            child,
            LocalTransform3D.Identity with
            {
                Position = new Vector3(0f, 2f, 0f),
            }
        );

        var system = new TransformHierarchySystem(world);
        system.Update(0f);
        Assert.True(world.TryGetComponent<Transform3D>(child, out var beforeDestroy));

        world.DestroyEntity(parent);
        system.Update(0f);

        Assert.False(world.TryGetComponent<Parent>(child, out _));
        Assert.True(world.TryGetComponent<Transform3D>(child, out var afterDestroy));
        AssertClose(beforeDestroy.Position, afterDestroy.Position);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
        Assert.Equal(expected.Z, actual.Z, Precision);
    }
}
