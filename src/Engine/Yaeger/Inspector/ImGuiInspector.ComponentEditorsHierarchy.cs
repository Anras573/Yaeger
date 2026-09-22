using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// Curated editors for the entity-hierarchy components: Parent, LocalTransform2D, LocalTransform3D.
// Parent gets an entity picker (set/change/clear) instead of the generic read-only registry
// fallback; the local transforms get numeric drag-editors mirroring
// ImGuiInspector.ComponentEditors2D.cs/ComponentEditors3D.cs's Transform2D/Transform3D sections.
// Reparenting reuses the same cycle-check and local-transform math as drag-and-drop reparenting
// (see EntityReparenting.cs, ImGuiInspector.Hierarchy.cs) so both paths behave identically.
public sealed partial class ImGuiInspector
{
    // Which candidate the Parent picker's combo currently has selected, cached per selected
    // entity so it survives across frames until the user picks something else or selection moves
    // to a different entity.
    private Entity? _parentPickerForEntity;
    private Entity? _parentPickerSelection;

    // Separate from ComponentEditors3D.cs's Transform3D rotation cache — an entity can carry both
    // a Transform3D and a LocalTransform3D at once, and each needs its own Euler-angle cache.
    private Entity? _localRotationCacheEntity;
    private Quaternion _localRotationCacheQuat;
    private Vector3 _localRotationCacheEulerDeg;

    private void DrawParentSection(Entity entity)
    {
        if (!ImGui.CollapsingHeader("Parent"))
            return;

        var hasParent = _world.TryGetComponent<Parent>(entity, out var parent);
        ImGui.Text(
            hasParent
                ? $"Current: {EntityDisplayLabel(parent.ParentEntity)}"
                : "Current: (none — root entity)"
        );

        // A candidate is anyone but the entity itself, and anyone whose subtree the entity isn't
        // already part of — either would create a Parent cycle, the same constraint
        // TransformHierarchySystem.Update enforces at runtime by throwing.
        var candidates = _world
            .Entities.Where(candidate =>
                candidate != entity
                && !EntityReparenting.WouldCreateCycle(_world, entity, candidate)
            )
            .OrderBy(candidate => candidate.Id)
            .ToArray();

        if (candidates.Length == 0)
        {
            ImGui.TextDisabled("No other entities available to parent to.");
        }
        else
        {
            if (
                _parentPickerForEntity != entity
                || _parentPickerSelection is not { } cached
                || !candidates.Contains(cached)
            )
            {
                _parentPickerForEntity = entity;
                _parentPickerSelection =
                    hasParent && candidates.Contains(parent.ParentEntity)
                        ? parent.ParentEntity
                        : candidates[0];
            }

            var selection = _parentPickerSelection!.Value;
            ImGui.SetNextItemWidth(220);
            if (ImGui.BeginCombo($"##parentPick_{entity.Id}", EntityDisplayLabel(selection)))
            {
                foreach (var candidate in candidates)
                {
                    if (ImGui.Selectable(EntityDisplayLabel(candidate), candidate == selection))
                        _parentPickerSelection = candidate;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();

            var isNoOp = hasParent && parent.ParentEntity == selection;
            if (isNoOp)
                ImGui.BeginDisabled();
            if (ImGui.SmallButton($"Set##parentSet_{entity.Id}"))
                _pendingWorldOps.Add(w => ReparentTo(w, entity, selection));
            if (isNoOp)
                ImGui.EndDisabled();
        }

        if (hasParent)
        {
            ImGui.Spacing();
            if (ImGui.SmallButton($"Clear##parentClear_{entity.Id}"))
                _pendingWorldOps.Add(w => w.RemoveComponent<Parent>(entity));
        }

        ImGui.Spacing();
    }

    private void DrawLocalTransform2DSection(Entity entity, LocalTransform2D t)
    {
        if (!ImGui.CollapsingHeader("LocalTransform2D"))
            return;

        var posX = t.Position.X;
        var posY = t.Position.Y;
        var rot = t.Rotation;
        var scaleX = t.Scale.X;
        var scaleY = t.Scale.Y;
        var changed = false;

        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"X##ltPosX_{entity.Id}", ref posX, 0.01f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"Y##ltPosY_{entity.Id}", ref posY, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Position");

        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Rotation##ltRot_{entity.Id}", ref rot, 0.001f);

        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"X##ltScX_{entity.Id}", ref scaleX, 0.01f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"Y##ltScY_{entity.Id}", ref scaleY, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Scale");

        if (changed)
        {
            t.Position = new Vector2(posX, posY);
            t.Rotation = rot;
            t.Scale = new Vector2(scaleX, scaleY);
            var snapshot = t;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##LocalTransform2D_{entity.Id}"))
            ScheduleRemove(entity, typeof(LocalTransform2D));

        ImGui.Spacing();
    }

    private void DrawLocalTransform3DSection(Entity entity, LocalTransform3D t)
    {
        if (!ImGui.CollapsingHeader("LocalTransform3D"))
            return;

        var position = t.Position;
        var scale = t.Scale;
        var changed = false;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Position##lt3pos_{entity.Id}", ref position, 0.01f);

        // Edit rotation as Euler degrees, re-deriving from the quaternion only when the cached
        // value goes stale (selection change or external mutation) — mirrors
        // DrawTransform3DSection's own rotation cache in ComponentEditors3D.cs.
        if (_localRotationCacheEntity != entity || _localRotationCacheQuat != t.Rotation)
        {
            _localRotationCacheEntity = entity;
            _localRotationCacheQuat = t.Rotation;
            _localRotationCacheEulerDeg = ToEulerDegrees(t.Rotation);
        }

        var euler = _localRotationCacheEulerDeg;
        ImGui.SetNextItemWidth(220);
        var rotationChanged = ImGui.DragFloat3($"Rotation°##lt3rot_{entity.Id}", ref euler, 0.5f);
        if (rotationChanged)
            _localRotationCacheEulerDeg = euler;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Scale##lt3scl_{entity.Id}", ref scale, 0.01f);

        if (changed || rotationChanged)
        {
            if (rotationChanged)
            {
                var rotation = FromEulerDegrees(_localRotationCacheEulerDeg);
                _localRotationCacheQuat = rotation;
                t.Rotation = rotation;
            }
            t.Position = position;
            t.Scale = scale;
            var snapshot = t;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##LocalTransform3D_{entity.Id}"))
            ScheduleRemove(entity, typeof(LocalTransform3D));

        ImGui.Spacing();
    }
}
