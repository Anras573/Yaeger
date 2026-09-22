using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// Drag-and-drop reparenting for the entity tree drawn by ImGuiInspector.MainWindow.cs. Dropping
// an entity node onto another re-targets its Parent and recomputes a LocalTransform2D/
// LocalTransform3D so it doesn't visually jump; dropping onto the "Entities" header removes
// Parent, making the entity a root again. The transform math and cycle detection live in the pure,
// unit-tested EntityReparenting; mutation is routed through the existing _pendingWorldOps queue
// (see ImGuiInspector.Commands.cs) rather than touching _world mid-frame.
public sealed partial class ImGuiInspector
{
    private const string EntityDragDropPayloadType = "YaegerEntityId";

    // Call immediately after drawing the item (e.g. the TreeNodeEx call) that should act as the
    // drag handle.
    private static unsafe void BeginEntityDragSource(Entity entity, string label)
    {
        if (!ImGui.BeginDragDropSource())
            return;

        var id = entity.Id;
        ImGui.SetDragDropPayload(EntityDragDropPayloadType, (IntPtr)(&id), sizeof(int));
        ImGui.Text(label);
        ImGui.EndDragDropSource();
    }

    // Call immediately after drawing the item that should act as a drop target.
    // newParent == null means "drop here to make the entity a root" (removes Parent).
    private unsafe void AcceptEntityDrop(Entity? newParent)
    {
        if (!ImGui.BeginDragDropTarget())
            return;

        var payload = ImGui.AcceptDragDropPayload(EntityDragDropPayloadType);
        if (payload.NativePtr != null)
        {
            var dropped = new Entity(*(int*)payload.Data);
            ScheduleReparent(dropped, newParent);
        }

        ImGui.EndDragDropTarget();
    }

    private void ScheduleReparent(Entity dropped, Entity? newParent)
    {
        if (newParent is not { } parent)
        {
            _pendingWorldOps.Add(w => w.RemoveComponent<Parent>(dropped));
            return;
        }

        // Reject a drop onto itself or onto one of its own descendants — either would create a
        // cycle that TransformHierarchySystem.Update can only detect by throwing.
        if (EntityReparenting.WouldCreateCycle(_world, dropped, parent))
            return;

        _pendingWorldOps.Add(w => ReparentTo(w, dropped, parent));
    }

    private static void ReparentTo(World world, Entity dropped, Entity parent)
    {
        if (
            world.TryGetComponent<Transform2D>(dropped, out var childWorld2D)
            && world.TryGetComponent<Transform2D>(parent, out var parentWorld2D)
        )
            world.AddComponent(dropped, EntityReparenting.ToLocal2D(parentWorld2D, childWorld2D));

        if (
            world.TryGetComponent<Transform3D>(dropped, out var childWorld3D)
            && world.TryGetComponent<Transform3D>(parent, out var parentWorld3D)
        )
            world.AddComponent(dropped, EntityReparenting.ToLocal3D(parentWorld3D, childWorld3D));

        world.AddComponent(dropped, new Parent(parent));
    }
}
