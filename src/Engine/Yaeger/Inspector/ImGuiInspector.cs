using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Windowing;

namespace Yaeger.Inspector;

/// <summary>
/// In-game ImGui overlay for live entity inspection and editing.
/// Wire it up in your render loop and toggle with a key binding:
/// <code>
/// var inspector = new ImGuiInspector(window, world, componentRegistry);
/// window.OnRender += delta => { renderSystem.Render(); inspector.Render(delta); };
/// Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);
/// </code>
/// The inspector renders after your game systems so the overlay sits on top.
/// </summary>
public sealed class ImGuiInspector : IDisposable
{
    private readonly World _world;
    private readonly ComponentRegistry? _registry;
    private readonly SceneSaver? _sceneSaver;
    private readonly ImGuiController _controller;

    private bool _visible;
    private Entity? _selectedEntity;
    private int _addComponentComboIndex;
    private string _saveScenePath = "Scenes/scene.json";
    private string _saveStatusMessage = string.Empty;

    // Deferred commands — mutations run after all ImGui draw calls to avoid iterator invalidation.
    // A List is used so multiple operations queued in the same frame (e.g. remove + add) all execute.
    private Entity? _pendingDestroyEntity;
    private readonly List<Action<World>> _pendingWorldOps = [];

    // TypeIds that can be added with a sensible zero/default value
    private static readonly Dictionary<string, Action<World, Entity>> DefaultAddActions = new()
    {
        ["Transform2D"] = static (w, e) => w.AddComponent(e, new Transform2D(Vector2.Zero)),
        ["Camera2D"] = static (w, e) => w.AddComponent(e, new Camera2D()),
        ["AnimationState"] = static (w, e) => w.AddComponent(e, default(AnimationState)),
        ["RenderLayer"] = static (w, e) => w.AddComponent(e, default(RenderLayer)),
    };

    private static readonly MethodInfo WorldRemoveMethod = typeof(World).GetMethod(
        nameof(World.RemoveComponent)
    )!;

    public ImGuiInspector(Window window, World world, ComponentRegistry? registry = null)
    {
        _world = world;
        _registry = registry;
        _sceneSaver = registry is not null ? new SceneSaver(registry) : null;
        _controller = new ImGuiController(window.Gl, window.InnerView, window.InputContext);
    }

    /// <summary>Toggles the inspector overlay on or off.</summary>
    public void Toggle() => _visible = !_visible;

    /// <summary>
    /// Updates and renders the inspector overlay.  Call inside <c>OnRender</c> after all game
    /// rendering so the overlay sits on top.
    /// </summary>
    public void Render(double delta)
    {
        _controller.Update((float)delta);

        if (_visible)
            DrawInspectorWindow();

        _controller.Render();
        FlushPendingCommands();
    }

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
            _pendingWorldOps.Add(static w => w.CreateEntity());
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

        // ── Other registered components (read-only + remove button) ──────────
        if (_registry != null)
        {
            foreach (var serializer in _registry.Serializers)
            {
                if (serializer.TypeId is "Transform2D" or "Camera2D" or "Sprite")
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

    // ── Curated component editors ─────────────────────────────────────────────

    private void DrawTransform2DSection(Entity entity, Transform2D t)
    {
        if (!ImGui.CollapsingHeader("Transform2D"))
            return;

        var posX = t.Position.X;
        var posY = t.Position.Y;
        var rot = t.Rotation;
        var scaleX = t.Scale.X;
        var scaleY = t.Scale.Y;
        var changed = false;

        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"X##posX_{entity.Id}", ref posX, 0.01f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"Y##posY_{entity.Id}", ref posY, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Position");

        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Rotation##rot_{entity.Id}", ref rot, 0.001f);

        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"X##scX_{entity.Id}", ref scaleX, 0.01f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"Y##scY_{entity.Id}", ref scaleY, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Scale");

        if (changed)
        {
            t.Position = new Vector2(posX, posY);
            t.Rotation = rot;
            t.Scale = new Vector2(scaleX, scaleY);
            _world.AddComponent(entity, t);
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Transform2D_{entity.Id}"))
            ScheduleRemove(entity, typeof(Transform2D));

        ImGui.Spacing();
    }

    private void DrawCamera2DSection(Entity entity, Camera2D cam)
    {
        if (!ImGui.CollapsingHeader("Camera2D"))
            return;

        var posX = cam.Position.X;
        var posY = cam.Position.Y;
        var zoom = cam.Zoom;
        var rot = cam.Rotation;
        var changed = false;

        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"X##camX_{entity.Id}", ref posX, 0.01f);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75);
        changed |= ImGui.DragFloat($"Y##camY_{entity.Id}", ref posY, 0.01f);
        ImGui.SameLine();
        ImGui.Text("Position");

        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Zoom##camZ_{entity.Id}", ref zoom, 0.01f, 0.01f, 100f);

        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Rotation##camR_{entity.Id}", ref rot, 0.001f);

        if (changed)
        {
            cam.Position = new Vector2(posX, posY);
            cam.Zoom = zoom;
            cam.Rotation = rot;
            _world.AddComponent(entity, cam);
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Camera2D_{entity.Id}"))
            ScheduleRemove(entity, typeof(Camera2D));

        ImGui.Spacing();
    }

    private void DrawSpriteSection(Entity entity, Sprite sprite)
    {
        if (!ImGui.CollapsingHeader("Sprite"))
            return;

        var path = sprite.TexturePath ?? string.Empty;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText($"TexturePath##sp_{entity.Id}", ref path, 512))
            _world.AddComponent(entity, new Sprite(path, sprite.Tint));

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Sprite_{entity.Id}"))
            ScheduleRemove(entity, typeof(Sprite));

        ImGui.Spacing();
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

        ImGui.SetNextItemWidth(180);
        ImGui.Combo(
            $"##addCombo_{entity.Id}",
            ref _addComponentComboIndex,
            typeIds,
            typeIds.Length
        );
        ImGui.SameLine();

        var selectedId = typeIds[_addComponentComboIndex];
        var hasDefaultAction = DefaultAddActions.ContainsKey(selectedId);

        if (hasDefaultAction)
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

        // Camera2D is a curated inspector type but not in the default registry
        if (!_world.TryGetComponent<Camera2D>(entity, out _))
            items.Add("Camera2D");

        if (_registry != null)
        {
            foreach (var serializer in _registry.Serializers)
            {
                // Only show types the entity doesn't already have
                if (serializer.TrySerialize(_world, entity) == null)
                    items.Add(serializer.TypeId);
            }
        }

        return items.ToArray();
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly MethodInfo WorldTryGetComponentMethod = typeof(World).GetMethod(
        nameof(World.TryGetComponent)
    )!;

    /// <summary>
    /// Checks whether <paramref name="entity"/> carries the component handled by
    /// <paramref name="serializer"/>.  When <see cref="IComponentSerializer.ComponentType"/>
    /// is known, reflection is used for an authoritative presence check — this handles
    /// serializers that return <c>null</c> from TrySerialize even when the entity has the
    /// component (e.g. load-only / write-unsupported serializers).  Falls back to
    /// TrySerialize for serializers that don't expose a ComponentType.
    /// </summary>
    private bool EntityHasComponent(Entity entity, IComponentSerializer serializer)
    {
        if (serializer.ComponentType is { } type && type.IsValueType)
        {
            var method = WorldTryGetComponentMethod.MakeGenericMethod(type);
            var args = new object?[] { entity, null };
            return (bool)method.Invoke(_world, args)!;
        }

        return serializer.TrySerialize(_world, entity) != null;
    }

    private void ScheduleRemove(Entity entity, Type componentType)
    {
        // componentType must be a struct (World.RemoveComponent<T> where T : struct)
        if (!componentType.IsValueType)
            return;

        _pendingWorldOps.Add(w =>
        {
            var method = WorldRemoveMethod.MakeGenericMethod(componentType);
            method.Invoke(w, [entity]);
        });
    }

    private void FlushPendingCommands()
    {
        // World ops run first so that an Add queued in the same frame as Destroy
        // doesn't create zombie components on an entity that no longer exists.
        foreach (var op in _pendingWorldOps)
            op(_world);
        _pendingWorldOps.Clear();

        if (_pendingDestroyEntity.HasValue)
        {
            _world.DestroyEntity(_pendingDestroyEntity.Value);
            _pendingDestroyEntity = null;
        }
    }

    public void Dispose() => _controller.Dispose();
}
