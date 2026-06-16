using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Windowing;

namespace Yaeger.Inspector;

/// <summary>
/// In-game ImGui overlay for live entity inspection and editing. Lists every entity in the world
/// and provides curated editors for both the 2D components (<see cref="Transform2D"/>,
/// <see cref="Camera2D"/>, <see cref="Sprite"/>) and the 3D components (<see cref="Transform3D"/>,
/// <see cref="Camera3D"/>, <see cref="Material3D"/>, <see cref="MeshHandle"/>,
/// <see cref="DirectionalLight"/>, <see cref="PointLight"/>, <see cref="SpotLight"/>), so it doubles
/// as a lightweight 3D scene editor. Edits are applied live to the world on the same frame.
/// Wire it up in your render loop and toggle with a key binding:
/// <code>
/// var inspector = new ImGuiInspector(window, world, componentRegistry);
/// window.OnRender += delta => { meshRenderSystem.Render(); inspector.Render(delta); };
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

    // Per-entity Euler-angle cache for editing Transform3D rotations. Quaternions are awkward to
    // edit directly, so the UI exposes Euler degrees instead. We cache the derived Euler value and
    // only re-derive it from the quaternion when the selection changes or the quaternion is mutated
    // externally — this keeps small successive drag edits from drifting through repeated
    // quaternion↔Euler round-trips.
    private Entity? _rotationCacheEntity;
    private Quaternion _rotationCacheQuat;
    private Vector3 _rotationCacheEulerDeg;

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
        ["PointLight"] = static (w, e) => w.AddComponent(e, PointLight.Default),
        ["SpotLight"] = static (w, e) => w.AddComponent(e, SpotLight.Default),
    };

    // Curated 3D component type ids handled by their own editor sections below.
    private static readonly string[] Curated3DTypeIds =
    [
        "Transform3D",
        "Camera3D",
        "MeshHandle",
        "Material3D",
        "DirectionalLight",
        "PointLight",
        "SpotLight",
    ];

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
        if (!_visible)
        {
            // Still flush any ops queued on the frame visibility was toggled off
            FlushPendingCommands();
            return;
        }

        _controller.Update((float)delta);
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
            var snapshot = t;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
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
            var snapshot = cam;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
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
        {
            var tint = sprite.Tint;
            var newPath = path;
            _pendingWorldOps.Add(w => w.AddComponent(entity, new Sprite(newPath, tint)));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Sprite_{entity.Id}"))
            ScheduleRemove(entity, typeof(Sprite));

        ImGui.Spacing();
    }

    // ── Curated 3D component editors ──────────────────────────────────────────

    private void DrawTransform3DSection(Entity entity, Transform3D t)
    {
        if (!ImGui.CollapsingHeader("Transform3D"))
            return;

        var position = t.Position;
        var scale = t.Scale;
        var changed = false;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Position##t3pos_{entity.Id}", ref position, 0.01f);

        // Edit rotation as Euler degrees, re-deriving from the quaternion only when the cached
        // value goes stale (selection change or external mutation) — see _rotationCache* fields.
        if (_rotationCacheEntity != entity || _rotationCacheQuat != t.Rotation)
        {
            _rotationCacheEntity = entity;
            _rotationCacheQuat = t.Rotation;
            _rotationCacheEulerDeg = ToEulerDegrees(t.Rotation);
        }

        var euler = _rotationCacheEulerDeg;
        ImGui.SetNextItemWidth(220);
        var rotationChanged = ImGui.DragFloat3($"Rotation°##t3rot_{entity.Id}", ref euler, 0.5f);
        if (rotationChanged)
            _rotationCacheEulerDeg = euler;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Scale##t3scl_{entity.Id}", ref scale, 0.01f);

        if (changed || rotationChanged)
        {
            var rotation = FromEulerDegrees(_rotationCacheEulerDeg);
            // Keep the cache in step with what we just wrote so the next frame doesn't re-derive.
            _rotationCacheQuat = rotation;
            t.Position = position;
            t.Rotation = rotation;
            t.Scale = scale;
            var snapshot = t;
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Transform3D_{entity.Id}"))
            ScheduleRemove(entity, typeof(Transform3D));

        ImGui.Spacing();
    }

    private void DrawCamera3DSection(Entity entity, Camera3D cam)
    {
        if (!ImGui.CollapsingHeader("Camera3D"))
            return;

        var position = cam.Position;
        var target = cam.Target;
        var up = cam.Up;
        var fovDeg = cam.Fov * (180f / MathF.PI);
        var near = cam.Near;
        var far = cam.Far;
        var changed = false;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Position##c3pos_{entity.Id}", ref position, 0.01f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Target##c3tgt_{entity.Id}", ref target, 0.01f);
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Up##c3up_{entity.Id}", ref up, 0.01f);

        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"FOV°##c3fov_{entity.Id}", ref fovDeg, 0.2f, 1f, 179f);
        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Near##c3near_{entity.Id}", ref near, 0.01f, 0.001f, far);
        ImGui.SetNextItemWidth(120);
        changed |= ImGui.DragFloat($"Far##c3far_{entity.Id}", ref far, 1f, near, 100000f);

        if (changed)
        {
            var snapshot = cam with
            {
                Position = position,
                Target = target,
                Up = up,
                Fov = fovDeg * (MathF.PI / 180f),
                Near = near,
                Far = far,
            };
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Camera3D_{entity.Id}"))
            ScheduleRemove(entity, typeof(Camera3D));

        ImGui.Spacing();
    }

    private void DrawMeshHandleSection(Entity entity, MeshHandle mesh)
    {
        if (!ImGui.CollapsingHeader("MeshHandle"))
            return;

        // The id is an opaque key into a mesh registry — editable values would point at arbitrary
        // (or non-existent) meshes, so it is surfaced read-only.
        ImGui.Text($"Mesh Id: {mesh.Id}");
        ImGui.TextDisabled("(assigned in code)");

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##MeshHandle_{entity.Id}"))
            ScheduleRemove(entity, typeof(MeshHandle));

        ImGui.Spacing();
    }

    private void DrawMaterial3DSection(Entity entity, Material3D mat)
    {
        if (!ImGui.CollapsingHeader("Material3D"))
            return;

        var changed = false;
        var usePbr = mat.UsePbr;
        changed |= ImGui.Checkbox($"Use PBR##m3pbr_{entity.Id}", ref usePbr);

        if (usePbr)
        {
            var metallic = mat.MetallicFactor;
            var roughness = mat.RoughnessFactor;
            ImGui.SetNextItemWidth(160);
            changed |= ImGui.SliderFloat($"Metallic##m3met_{entity.Id}", ref metallic, 0f, 1f);
            ImGui.SetNextItemWidth(160);
            changed |= ImGui.SliderFloat($"Roughness##m3rgh_{entity.Id}", ref roughness, 0f, 1f);
            var emissive = mat.EmissiveColor;
            changed |= ColorEdit3($"Emissive##m3emi_{entity.Id}", ref emissive);

            if (changed)
            {
                var snapshot = mat with
                {
                    UsePbr = usePbr,
                    MetallicFactor = metallic,
                    RoughnessFactor = roughness,
                    EmissiveColor = emissive,
                };
                _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
            }
        }
        else
        {
            var ambient = mat.Ambient;
            var diffuse = mat.Diffuse;
            var specular = mat.Specular;
            var shininess = mat.Shininess;
            changed |= ColorEdit3($"Ambient##m3amb_{entity.Id}", ref ambient);
            changed |= ColorEdit3($"Diffuse##m3dif_{entity.Id}", ref diffuse);
            changed |= ColorEdit3($"Specular##m3spc_{entity.Id}", ref specular);
            ImGui.SetNextItemWidth(160);
            changed |= ImGui.DragFloat(
                $"Shininess##m3shn_{entity.Id}",
                ref shininess,
                0.5f,
                0f,
                512f
            );

            if (changed)
            {
                var snapshot = mat with
                {
                    UsePbr = usePbr,
                    Ambient = ambient,
                    Diffuse = diffuse,
                    Specular = specular,
                    Shininess = shininess,
                };
                _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
            }
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##Material3D_{entity.Id}"))
            ScheduleRemove(entity, typeof(Material3D));

        ImGui.Spacing();
    }

    private void DrawDirectionalLightSection(Entity entity, DirectionalLight light)
    {
        if (!ImGui.CollapsingHeader("DirectionalLight"))
            return;

        var direction = light.Direction;
        var color = light.Color;
        var intensity = light.Intensity;
        var changed = false;

        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Direction##dl_dir_{entity.Id}", ref direction, 0.01f);
        changed |= ColorEdit3($"Color##dl_col_{entity.Id}", ref color);
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat(
            $"Intensity##dl_int_{entity.Id}",
            ref intensity,
            0.01f,
            0f,
            100f
        );

        if (changed)
        {
            var snapshot = light with
            {
                Direction = direction,
                Color = color,
                Intensity = intensity,
            };
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##DirectionalLight_{entity.Id}"))
            ScheduleRemove(entity, typeof(DirectionalLight));

        ImGui.Spacing();
    }

    private void DrawPointLightSection(Entity entity, PointLight light)
    {
        if (!ImGui.CollapsingHeader("PointLight"))
            return;

        if (!_world.TryGetComponent<Transform3D>(entity, out _))
            ImGui.TextDisabled("No Transform3D — light has no world position.");

        var color = light.Color;
        var intensity = light.Intensity;
        var range = light.Range;
        var changed = false;

        changed |= ColorEdit3($"Color##pl_col_{entity.Id}", ref color);
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat(
            $"Intensity##pl_int_{entity.Id}",
            ref intensity,
            0.01f,
            0f,
            100f
        );
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat($"Range##pl_rng_{entity.Id}", ref range, 0.05f, 0f, 1000f);

        if (changed)
        {
            var snapshot = light with { Color = color, Intensity = intensity, Range = range };
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##PointLight_{entity.Id}"))
            ScheduleRemove(entity, typeof(PointLight));

        ImGui.Spacing();
    }

    private void DrawSpotLightSection(Entity entity, SpotLight light)
    {
        if (!ImGui.CollapsingHeader("SpotLight"))
            return;

        if (!_world.TryGetComponent<Transform3D>(entity, out _))
            ImGui.TextDisabled("No Transform3D — light has no world position.");

        var color = light.Color;
        var intensity = light.Intensity;
        var direction = light.Direction;
        var range = light.Range;
        var innerDeg = light.InnerConeAngle * (180f / MathF.PI);
        var outerDeg = light.OuterConeAngle * (180f / MathF.PI);
        var changed = false;

        changed |= ColorEdit3($"Color##sl_col_{entity.Id}", ref color);
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat(
            $"Intensity##sl_int_{entity.Id}",
            ref intensity,
            0.01f,
            0f,
            100f
        );
        ImGui.SetNextItemWidth(220);
        changed |= ImGui.DragFloat3($"Direction##sl_dir_{entity.Id}", ref direction, 0.01f);
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat(
            $"Inner°##sl_inner_{entity.Id}",
            ref innerDeg,
            0.2f,
            0f,
            outerDeg
        );
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat(
            $"Outer°##sl_outer_{entity.Id}",
            ref outerDeg,
            0.2f,
            innerDeg,
            89f
        );
        ImGui.SetNextItemWidth(160);
        changed |= ImGui.DragFloat($"Range##sl_rng_{entity.Id}", ref range, 0.05f, 0f, 1000f);

        if (changed)
        {
            var snapshot = light with
            {
                Color = color,
                Intensity = intensity,
                Direction = direction,
                InnerConeAngle = innerDeg * (MathF.PI / 180f),
                OuterConeAngle = outerDeg * (MathF.PI / 180f),
                Range = range,
            };
            _pendingWorldOps.Add(w => w.AddComponent(entity, snapshot));
        }

        ImGui.Spacing();
        if (ImGui.SmallButton($"Remove##SpotLight_{entity.Id}"))
            ScheduleRemove(entity, typeof(SpotLight));

        ImGui.Spacing();
    }

    // ── Colour + rotation helpers ─────────────────────────────────────────────

    /// <summary>Edits an RGB <see cref="Color"/> (alpha preserved) via ImGui's colour picker.</summary>
    private static bool ColorEdit3(string label, ref Color color)
    {
        var v = color.ToVector4();
        var rgb = new Vector3(v.X, v.Y, v.Z);
        if (!ImGui.ColorEdit3(label, ref rgb))
            return false;
        color = Color.FromVector4(new Vector4(rgb, v.W));
        return true;
    }

    /// <summary>
    /// Extracts intrinsic Y-X-Z Euler angles (degrees) from a quaternion, matching the convention
    /// of <see cref="Quaternion.CreateFromYawPitchRoll"/>. Returned as (pitch X, yaw Y, roll Z).
    /// </summary>
    private static Vector3 ToEulerDegrees(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        var sinPitch = Math.Clamp(2f * (q.W * q.X - q.Y * q.Z), -1f, 1f);
        var pitch = MathF.Asin(sinPitch);
        var yaw = MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        var roll = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
        return new Vector3(pitch, yaw, roll) * (180f / MathF.PI);
    }

    /// <summary>Inverse of <see cref="ToEulerDegrees"/>: builds a quaternion from (pitch, yaw, roll) degrees.</summary>
    private static Quaternion FromEulerDegrees(Vector3 pitchYawRollDeg)
    {
        var r = pitchYawRollDeg * (MathF.PI / 180f);
        return Quaternion.CreateFromYawPitchRoll(r.Y, r.X, r.Z);
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
            // Pass a boxed default for the out parameter; null can misfire for value types
            var args = new object?[] { entity, Activator.CreateInstance(type) };
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
