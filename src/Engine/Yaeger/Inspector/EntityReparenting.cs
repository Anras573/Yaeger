using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

/// <summary>
/// Pure logic behind drag-and-drop reparenting in <see cref="ImGuiInspector"/>'s entity tree (see
/// <c>ImGuiInspector.Hierarchy.cs</c>): cycle detection and the <see cref="LocalTransform2D"/>/
/// <see cref="LocalTransform3D"/> math that keeps a reparented entity from visually jumping. Kept
/// free of ImGui/world-mutation concerns so it's unit-testable, the same way
/// <c>MeshInstanceBatcher</c>/<c>PostProcessPlanner</c>/<c>TilemapColliderMerger</c> are.
/// </summary>
public static class EntityReparenting
{
    /// <summary>
    /// True if setting <paramref name="candidate"/>'s parent to <paramref name="newParent"/> would
    /// create a <see cref="Parent"/> cycle — because they are the same entity, or
    /// <paramref name="newParent"/> is (transitively) a descendant of <paramref name="candidate"/>.
    /// Walks the <see cref="Parent"/> chain the same way
    /// <see cref="Systems.TransformHierarchySystem"/> detects cycles.
    /// </summary>
    public static bool WouldCreateCycle(World world, Entity candidate, Entity newParent)
    {
        var current = newParent;
        var visited = new HashSet<Entity>();
        while (visited.Add(current))
        {
            if (current == candidate)
                return true;
            if (!world.TryGetComponent<Parent>(current, out var parent))
                return false;
            current = parent.ParentEntity;
        }

        // A pre-existing cycle above newParent; treat it as unsafe to attach to rather than loop.
        return true;
    }

    /// <summary>
    /// Inverse of <see cref="Systems.TransformHierarchySystem"/>'s 2D composition: given the
    /// parent's current world transform and the child's desired world transform, returns the
    /// <see cref="LocalTransform2D"/> that composes back to it.
    /// </summary>
    public static LocalTransform2D ToLocal2D(Transform2D parentWorld, Transform2D childWorld)
    {
        var delta = childWorld.Position - parentWorld.Position;
        var cos = MathF.Cos(parentWorld.Rotation);
        var sin = MathF.Sin(parentWorld.Rotation);
        var unrotated = new Vector2(delta.X * cos + delta.Y * sin, -delta.X * sin + delta.Y * cos);

        return new LocalTransform2D(
            SafeDivide(unrotated, parentWorld.Scale),
            childWorld.Rotation - parentWorld.Rotation,
            SafeDivide(childWorld.Scale, parentWorld.Scale)
        );
    }

    /// <summary>
    /// Inverse of <see cref="Systems.TransformHierarchySystem"/>'s 3D composition.
    /// </summary>
    public static LocalTransform3D ToLocal3D(Transform3D parentWorld, Transform3D childWorld)
    {
        var inverseRotation = Quaternion.Inverse(parentWorld.Rotation);
        var delta = childWorld.Position - parentWorld.Position;

        return new LocalTransform3D(
            SafeDivide(Vector3.Transform(delta, inverseRotation), parentWorld.Scale),
            Quaternion.Normalize(inverseRotation * childWorld.Rotation),
            SafeDivide(childWorld.Scale, parentWorld.Scale)
        );
    }

    // A zero/near-zero parent scale component would otherwise divide out to Infinity/NaN and
    // corrupt the child's transform; fall back to the unscaled delta instead.
    private static Vector2 SafeDivide(Vector2 a, Vector2 b) =>
        new(SafeDivide(a.X, b.X), SafeDivide(a.Y, b.Y));

    private static Vector3 SafeDivide(Vector3 a, Vector3 b) =>
        new(SafeDivide(a.X, b.X), SafeDivide(a.Y, b.Y), SafeDivide(a.Z, b.Z));

    private static float SafeDivide(float a, float b) => MathF.Abs(b) < 1e-6f ? a : a / b;
}
