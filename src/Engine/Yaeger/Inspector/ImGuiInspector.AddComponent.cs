using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// The "Add Component" combo + button row at the bottom of the component inspector column (see
// ImGuiInspector.MainWindow.cs), and the default zero-value construction for each addable type.
public sealed partial class ImGuiInspector
{
    private int _addComponentComboIndex;

    // TypeIds that can be added with a sensible zero/default value
    private static readonly Dictionary<string, Action<World, Entity>> DefaultAddActions = new()
    {
        ["Transform2D"] = static (w, e) => w.AddComponent(e, new Transform2D(Vector2.Zero)),
        ["Camera2D"] = static (w, e) => w.AddComponent(e, new Camera2D()),
        ["AnimationState"] = static (w, e) => w.AddComponent(e, default(AnimationState)),
        ["RenderLayer"] = static (w, e) => w.AddComponent(e, default(RenderLayer)),
        ["Transform3D"] = static (w, e) => w.AddComponent(e, Transform3D.Identity),
        ["Camera3D"] = static (w, e) => w.AddComponent(e, Camera3D.Default),
        ["Material3D"] = static (w, e) => w.AddComponent(e, new Material3D()),
        ["DirectionalLight"] = static (w, e) => w.AddComponent(e, DirectionalLight.Default),
        // Point and spot lights are positioned by their Transform3D — MeshRenderSystem skips lights
        // without one — so ensure a transform exists, otherwise the freshly added light does nothing.
        ["PointLight"] = static (w, e) =>
        {
            EnsureTransform3D(w, e);
            w.AddComponent(e, PointLight.Default);
        },
        ["SpotLight"] = static (w, e) =>
        {
            EnsureTransform3D(w, e);
            w.AddComponent(e, SpotLight.Default);
        },
    };

    private static void EnsureTransform3D(World world, Entity entity)
    {
        if (!world.TryGetComponent<Transform3D>(entity, out _))
            world.AddComponent(entity, Transform3D.Identity);
    }

    // ── Add component row ────────────────────────────────────────────────────

    private void DrawAddComponentRow(Entity entity)
    {
        var typeIds = BuildAddableTypeIds(entity);
        if (typeIds.Length == 0)
            return;

        ImGui.Text("Add Component");

        if (_addComponentComboIndex >= typeIds.Length)
            _addComponentComboIndex = 0;

        var currentLabel = typeIds[_addComponentComboIndex];
        ImGui.SetNextItemWidth(180);
        if (ImGui.BeginCombo($"##addCombo_{entity.Id}", currentLabel))
        {
            for (var i = 0; i < typeIds.Length; i++)
            {
                var typeId = typeIds[i];
                var supported = DefaultAddActions.ContainsKey(typeId);
                if (!supported)
                    ImGui.BeginDisabled();

                if (ImGui.Selectable(typeId, _addComponentComboIndex == i))
                    _addComponentComboIndex = i;

                if (!supported)
                    ImGui.EndDisabled();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();

        var selectedId = typeIds[_addComponentComboIndex];
        if (DefaultAddActions.ContainsKey(selectedId))
        {
            if (ImGui.SmallButton($"+##add_{entity.Id}"))
            {
                var addAction = DefaultAddActions[selectedId];
                _pendingWorldOps.Add(w => addAction(w, entity));
            }
        }
        else
        {
            ImGui.TextDisabled("(no default — add in code)");
        }
    }

    private string[] BuildAddableTypeIds(Entity entity)
    {
        var items = new List<string>();

        if (!_world.TryGetComponent<Transform2D>(entity, out _))
            items.Add("Transform2D");

        // Camera2D is not in the default registry, so it is always handled explicitly
        if (!_world.TryGetComponent<Camera2D>(entity, out _))
            items.Add("Camera2D");

        // 3D components are never in the serializer registry, so offer them explicitly. MeshHandle
        // is intentionally omitted — it needs a real mesh id, which can only be assigned in code.
        if (!_world.TryGetComponent<Transform3D>(entity, out _))
            items.Add("Transform3D");
        if (!_world.TryGetComponent<Camera3D>(entity, out _))
            items.Add("Camera3D");
        if (!_world.TryGetComponent<Material3D>(entity, out _))
            items.Add("Material3D");
        if (!_world.TryGetComponent<DirectionalLight>(entity, out _))
            items.Add("DirectionalLight");
        if (!_world.TryGetComponent<PointLight>(entity, out _))
            items.Add("PointLight");
        if (!_world.TryGetComponent<SpotLight>(entity, out _))
            items.Add("SpotLight");

        if (_registry != null)
        {
            foreach (var serializer in _registry.Serializers)
            {
                // Skip curated types already handled above to avoid duplicates
                if (serializer.TypeId is "Transform2D" or "Camera2D")
                    continue;
                if (Curated3DTypeIds.Contains(serializer.TypeId))
                    continue;
                if (!EntityHasComponent(entity, serializer))
                    items.Add(serializer.TypeId);
            }
        }

        return items.ToArray();
    }
}
