using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// The ImGui panel's layout: entity list, curated + registry-driven component column, and the save
// row. See ImGuiInspector.ComponentEditors2D.cs/ComponentEditors3D.cs for the curated per-component
// editors this draws, and ImGuiInspector.AddComponent.cs for the "Add Component" row.
public sealed partial class ImGuiInspector
{
    private string _saveScenePath = "Scenes/scene.json";
    private string _saveStatusMessage = string.Empty;

    // ── Main window ──────────────────────────────────────────────────────────────

    private void DrawInspectorWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(720, 520), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);

        var open = true;
        if (!ImGui.Begin("Scene Inspector", ref open))
        {
            ImGui.End();
            if (!open)
                _visible = false;
            return;
        }

        if (!open)
            _visible = false;

        // Two-column layout: entity list left, component inspector right
        ImGui.Columns(2, "inspector_columns");
        ImGui.SetColumnWidth(0, 210);

        DrawEntityListColumn();

        ImGui.NextColumn();

        DrawComponentInspectorColumn();

        ImGui.Columns(1);

        ImGui.Separator();
        var showGizmos = ShowGizmos;
        if (ImGui.Checkbox("Show selection gizmos", ref showGizmos))
            ShowGizmos = showGizmos;
        DrawSaveRow();

        ImGui.End();
    }

    // ── Entity list (left column) ─────────────────────────────────────────────

    private void DrawEntityListColumn()
    {
        ImGui.Text("Entities");
        ImGui.Separator();

        // Snapshot to a sorted list so we don't mutate during iteration
        var entities = _world.Entities.OrderBy(e => e.Id).ToArray();

        foreach (var entity in entities)
        {
            // Tags are user-provided; strip "##" so ImGui doesn't treat it as an ID separator.
            // The explicit "##entity_{id}" suffix gives each Selectable a unique stable ID.
            var display = _world.TryGetTag(entity, out var tag)
                ? $"{tag.Replace("##", "#-#")}  (#{entity.Id})"
                : $"Entity#{entity.Id}";
            var selectableId = $"{display}##entity_{entity.Id}";

            var selected = _selectedEntity == entity;
            if (ImGui.Selectable(selectableId, selected))
                _selectedEntity = entity;
        }

        ImGui.Separator();

        if (ImGui.SmallButton("New Entity"))
            _pendingWorldOps.Add(w => _selectedEntity = w.CreateEntity());
    }

    // ── Component inspector (right column) ───────────────────────────────────

    private void DrawComponentInspectorColumn()
    {
        if (!_selectedEntity.HasValue)
        {
            ImGui.TextDisabled("Select an entity.");
            return;
        }

        var entity = _selectedEntity.Value;

        // The game may have destroyed this entity externally between frames
        var entitySet = _world.Entities as ICollection<Entity>;
        if (!(entitySet?.Contains(entity) ?? _world.Entities.Contains(entity)))
        {
            _selectedEntity = null;
            ImGui.TextDisabled("Entity no longer exists.");
            return;
        }

        var entityLabel = _world.TryGetTag(entity, out var tag)
            ? $"\"{tag}\"  (#{entity.Id})"
            : $"Entity#{entity.Id}";

        ImGui.Text(entityLabel);
        ImGui.Separator();

        // ── Curated editable components ──────────────────────────────────────
        if (_world.TryGetComponent<Transform2D>(entity, out var transform))
            DrawTransform2DSection(entity, transform);

        if (_world.TryGetComponent<Camera2D>(entity, out var camera))
            DrawCamera2DSection(entity, camera);

        if (_world.TryGetComponent<Sprite>(entity, out var sprite))
            DrawSpriteSection(entity, sprite);

        // ── Curated 3D components ────────────────────────────────────────────
        if (_world.TryGetComponent<Transform3D>(entity, out var transform3D))
            DrawTransform3DSection(entity, transform3D);

        if (_world.TryGetComponent<Camera3D>(entity, out var camera3D))
            DrawCamera3DSection(entity, camera3D);

        if (_world.TryGetComponent<MeshHandle>(entity, out var meshHandle))
            DrawMeshHandleSection(entity, meshHandle);

        if (_world.TryGetComponent<Material3D>(entity, out var material3D))
            DrawMaterial3DSection(entity, material3D);

        if (_world.TryGetComponent<DirectionalLight>(entity, out var directionalLight))
            DrawDirectionalLightSection(entity, directionalLight);

        if (_world.TryGetComponent<PointLight>(entity, out var pointLight))
            DrawPointLightSection(entity, pointLight);

        if (_world.TryGetComponent<SpotLight>(entity, out var spotLight))
            DrawSpotLightSection(entity, spotLight);

        // ── Other registered components (read-only + remove button) ──────────
        if (_registry != null)
        {
            foreach (var serializer in _registry.Serializers)
            {
                if (serializer.TypeId is "Transform2D" or "Camera2D" or "Sprite")
                    continue;
                if (Curated3DTypeIds.Contains(serializer.TypeId))
                    continue;

                if (!EntityHasComponent(entity, serializer))
                    continue;

                if (ImGui.CollapsingHeader(serializer.TypeId))
                {
                    var json = serializer.TrySerialize(_world, entity);
                    if (json != null)
                        ImGui.TextWrapped(json.ToJsonString());
                    else
                        ImGui.TextDisabled("(serialization not supported)");

                    ImGui.Spacing();

                    if (
                        serializer.ComponentType is { } cType
                        && cType.IsValueType
                        && ImGui.SmallButton($"Remove##{serializer.TypeId}_{entity.Id}")
                    )
                    {
                        ScheduleRemove(entity, cType);
                    }
                }
            }
        }

        ImGui.Spacing();
        DrawAddComponentRow(entity);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.15f, 0.15f, 1f));
        if (ImGui.Button($"Destroy Entity##destroy_{entity.Id}"))
        {
            _pendingDestroyEntity = entity;
            _selectedEntity = null;
        }
        ImGui.PopStyleColor();
    }

    // ── Save scene row ────────────────────────────────────────────────────────

    private void DrawSaveRow()
    {
        ImGui.SetNextItemWidth(300);
        ImGui.InputText("##savePath", ref _saveScenePath, 512);
        ImGui.SameLine();

        var canSave = _sceneSaver != null;

        if (!canSave)
            ImGui.BeginDisabled();

        if (ImGui.SmallButton("Save Scene") && canSave)
        {
            try
            {
                var dir = Path.GetDirectoryName(AssetPath.Resolve(_saveScenePath));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                _sceneSaver!.Save(_world, _saveScenePath);
                _saveStatusMessage = $"Saved at {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _saveStatusMessage = $"Error: {ex.Message}";
            }
        }

        if (!canSave)
            ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(_saveStatusMessage))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(_saveStatusMessage);
        }
    }
}
