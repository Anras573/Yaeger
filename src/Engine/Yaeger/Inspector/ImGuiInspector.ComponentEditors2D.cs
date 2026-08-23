using System.Numerics;
using ImGuiNET;
using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

// Curated editors for the 2D components: Transform2D, Camera2D, Sprite. See
// ImGuiInspector.ComponentEditors3D.cs for the 3D counterparts.
public sealed partial class ImGuiInspector
{
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
}
