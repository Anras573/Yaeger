using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// Curated editors for the 3D components: Transform3D, Camera3D, MeshHandle, Material3D, and the
// three light types. See ImGuiInspector.ComponentEditors2D.cs for the 2D counterparts.
public sealed partial class ImGuiInspector
{
    // Per-entity Euler-angle cache for editing Transform3D rotations. Quaternions are awkward to
    // edit directly, so the UI exposes Euler degrees instead. We cache the derived Euler value and
    // only re-derive it from the quaternion when the selection changes or the quaternion is mutated
    // externally — this keeps small successive drag edits from drifting through repeated
    // quaternion↔Euler round-trips.
    private Entity? _rotationCacheEntity;
    private Quaternion _rotationCacheQuat;
    private Vector3 _rotationCacheEulerDeg;

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
            // Only rebuild the quaternion when rotation was actually edited. Recomputing it on a
            // position/scale-only edit would push t.Rotation through a lossy Euler round-trip and
            // perturb an orientation the user never touched.
            if (rotationChanged)
            {
                var rotation = FromEulerDegrees(_rotationCacheEulerDeg);
                // Keep the cache in step with what we just wrote so the next frame doesn't re-derive.
                _rotationCacheQuat = rotation;
                t.Rotation = rotation;
            }
            t.Position = position;
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
        // Mirror Camera3D's own guards so the controls stay usable (and the displayed FOV matches
        // what the renderer uses) even for a default/invalid camera with zero Fov/Near/Far.
        var fovDeg = (cam.Fov > 0f ? cam.Fov : MathF.PI / 4f) * (180f / MathF.PI);
        var near = cam.Near > 0f ? cam.Near : 0.1f;
        var far = cam.Far > near ? cam.Far : near + 1000f;
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
            var emissiveIntensity = mat.EmissiveIntensity;
            ImGui.SetNextItemWidth(160);
            changed |= ImGui.DragFloat(
                $"Emissive Intensity##m3emint_{entity.Id}",
                ref emissiveIntensity,
                0.05f,
                0f,
                100f
            );

            if (changed)
            {
                var snapshot = mat with
                {
                    UsePbr = usePbr,
                    MetallicFactor = metallic,
                    RoughnessFactor = roughness,
                    EmissiveColor = emissive,
                    EmissiveIntensity = emissiveIntensity,
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
        // Guard against a zero-length or non-finite quaternion (e.g. default(Transform3D) has
        // Rotation = (0,0,0,0)): Quaternion.Normalize would yield NaNs that propagate to the UI.
        var lengthSq = q.LengthSquared();
        if (!float.IsFinite(lengthSq) || lengthSq < 1e-12f)
            return Vector3.Zero;

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
}
