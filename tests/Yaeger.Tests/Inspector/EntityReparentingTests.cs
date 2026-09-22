using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Inspector;

namespace Yaeger.Tests.Inspector;

public class EntityReparentingTests
{
    [Fact]
    public void WouldCreateCycle_SameEntity_ReturnsTrue()
    {
        var world = new World();
        var entity = world.CreateEntity();

        Assert.True(EntityReparenting.WouldCreateCycle(world, entity, entity));
    }

    [Fact]
    public void WouldCreateCycle_UnrelatedEntities_ReturnsFalse()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();

        Assert.False(EntityReparenting.WouldCreateCycle(world, a, b));
    }

    [Fact]
    public void WouldCreateCycle_NewParentIsDirectChild_ReturnsTrue()
    {
        var world = new World();
        var parent = world.CreateEntity();
        var child = world.CreateEntity();
        world.AddComponent(child, new Parent(parent));

        // Dropping `parent` onto its own child `child` would create parent -> child -> parent.
        Assert.True(EntityReparenting.WouldCreateCycle(world, parent, child));
    }

    [Fact]
    public void WouldCreateCycle_NewParentIsTransitiveDescendant_ReturnsTrue()
    {
        var world = new World();
        var grandparent = world.CreateEntity();
        var parent = world.CreateEntity();
        var grandchild = world.CreateEntity();
        world.AddComponent(parent, new Parent(grandparent));
        world.AddComponent(grandchild, new Parent(parent));

        Assert.True(EntityReparenting.WouldCreateCycle(world, grandparent, grandchild));
    }

    [Fact]
    public void WouldCreateCycle_NewParentIsUnrelatedDescendantOfSomeoneElse_ReturnsFalse()
    {
        var world = new World();
        var root = world.CreateEntity();
        var branchA = world.CreateEntity();
        var branchB = world.CreateEntity();
        world.AddComponent(branchA, new Parent(root));
        world.AddComponent(branchB, new Parent(root));

        Assert.False(EntityReparenting.WouldCreateCycle(world, branchA, branchB));
    }

    [Fact]
    public void ToLocal2D_IdentityParent_LocalMatchesWorld()
    {
        var parentWorld = new Transform2D(Vector2.Zero, 0f, Vector2.One);
        var childWorld = new Transform2D(new Vector2(3, 4), 0.5f, new Vector2(2, 2));

        var local = EntityReparenting.ToLocal2D(parentWorld, childWorld);

        Assert.Equal(childWorld.Position, local.Position);
        Assert.Equal(childWorld.Rotation, local.Rotation);
        Assert.Equal(childWorld.Scale, local.Scale);
    }

    [Fact]
    public void ToLocal2D_ThenComposedBack_ReproducesWorldTransform()
    {
        var parentWorld = new Transform2D(new Vector2(10, -5), MathF.PI / 4f, new Vector2(2, 3));
        var childWorld = new Transform2D(new Vector2(7, 2), 1.1f, new Vector2(1.5f, 0.5f));

        var local = EntityReparenting.ToLocal2D(parentWorld, childWorld);
        var recomposed = Compose2D(parentWorld, local);

        AssertApproximatelyEqual(childWorld.Position, recomposed.Position);
        Assert.Equal(childWorld.Rotation, recomposed.Rotation, 4);
        AssertApproximatelyEqual(childWorld.Scale, recomposed.Scale);
    }

    [Fact]
    public void ToLocal2D_ZeroParentScale_DoesNotProduceNaNOrInfinity()
    {
        var parentWorld = new Transform2D(Vector2.Zero, 0f, Vector2.Zero);
        var childWorld = new Transform2D(new Vector2(3, 4), 0f, new Vector2(2, 2));

        var local = EntityReparenting.ToLocal2D(parentWorld, childWorld);

        Assert.False(float.IsNaN(local.Position.X));
        Assert.False(float.IsInfinity(local.Position.X));
        Assert.False(float.IsNaN(local.Scale.X));
        Assert.False(float.IsInfinity(local.Scale.X));
    }

    [Fact]
    public void ToLocal3D_IdentityParent_LocalMatchesWorld()
    {
        var parentWorld = new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One);
        var childWorld = new Transform3D(
            new Vector3(1, 2, 3),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f),
            new Vector3(2, 2, 2)
        );

        var local = EntityReparenting.ToLocal3D(parentWorld, childWorld);

        AssertApproximatelyEqual(childWorld.Position, local.Position);
        AssertApproximatelyEqual(childWorld.Rotation, local.Rotation);
        AssertApproximatelyEqual(childWorld.Scale, local.Scale);
    }

    [Fact]
    public void ToLocal3D_ThenComposedBack_ReproducesWorldTransform()
    {
        var parentWorld = new Transform3D(
            new Vector3(5, 0, -2),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 3f),
            new Vector3(2, 1, 4)
        );
        var childWorld = new Transform3D(
            new Vector3(-1, 3, 2),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.7f),
            new Vector3(1, 1.5f, 0.5f)
        );

        var local = EntityReparenting.ToLocal3D(parentWorld, childWorld);
        var recomposed = Compose3D(parentWorld, local);

        AssertApproximatelyEqual(childWorld.Position, recomposed.Position);
        AssertApproximatelyEqual(childWorld.Rotation, recomposed.Rotation);
        AssertApproximatelyEqual(childWorld.Scale, recomposed.Scale);
    }

    // Mirrors TransformHierarchySystem.Compose2D/Compose3D, kept independent of the private
    // implementation there so this test verifies against the documented composition rule rather
    // than importing the same code it's checking the inverse of.
    private static Transform2D Compose2D(Transform2D parentWorld, LocalTransform2D local)
    {
        var scaledLocalPosition = local.Position * parentWorld.Scale;
        var cos = MathF.Cos(parentWorld.Rotation);
        var sin = MathF.Sin(parentWorld.Rotation);
        var rotatedLocalPosition = new Vector2(
            scaledLocalPosition.X * cos - scaledLocalPosition.Y * sin,
            scaledLocalPosition.X * sin + scaledLocalPosition.Y * cos
        );

        return new Transform2D(
            parentWorld.Position + rotatedLocalPosition,
            parentWorld.Rotation + local.Rotation,
            parentWorld.Scale * local.Scale
        );
    }

    private static Transform3D Compose3D(Transform3D parentWorld, LocalTransform3D local)
    {
        var scaledLocalPosition = local.Position * parentWorld.Scale;
        var rotatedLocalPosition = Vector3.Transform(scaledLocalPosition, parentWorld.Rotation);

        return new Transform3D(
            parentWorld.Position + rotatedLocalPosition,
            Quaternion.Normalize(parentWorld.Rotation * local.Rotation),
            parentWorld.Scale * local.Scale
        );
    }

    private static void AssertApproximatelyEqual(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
    }

    private static void AssertApproximatelyEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
    }

    private static void AssertApproximatelyEqual(Quaternion expected, Quaternion actual)
    {
        // A quaternion and its negation represent the same rotation.
        var dot = MathF.Abs(
            expected.X * actual.X
                + expected.Y * actual.Y
                + expected.Z * actual.Z
                + expected.W * actual.W
        );
        Assert.True(dot > 0.999f, $"Expected {expected} to approximately equal {actual}");
    }
}
