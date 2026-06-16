# Editor Overlay

`ImGuiInspector` is an in-game [Dear ImGui](https://github.com/ocornut/imgui) overlay that lists
every entity in a `World` and lets you live-edit the components attached to the selected one. It
works for both 2D and 3D scenes, so it doubles as a lightweight 3D scene editor for the
`Renderer3D` / `MeshRenderSystem` pipeline. Edits are applied to the world on the same frame, so
the change is visible immediately in the running game.

See it in action in [`Samples/CornellBox`](../Samples/CornellBox) — press **F1** to toggle the
overlay, then drag entity values around while the scene renders.

## Wiring it up

The inspector renders *after* your game systems so the overlay sits on top. Construct it once,
render it from `OnRender`, and bind a key to toggle it:

```csharp
using var window = Window.Create();
var world = new World();

// ... build your scene, renderer3D, meshRenderSystem ...

using var inspector = new ImGuiInspector(window, world);

Keyboard.AddKeyDown(Keys.F1, inspector.Toggle);

window.OnRender += delta =>
{
    meshRenderSystem.Render();
    inspector.Render(delta);   // draw last so it overlays the scene
};

window.Run();
```

`Render(delta)` must be called every frame even while the overlay is hidden — it flushes any
edits queued on the frame the overlay was toggled off. `Dispose()` (handled by `using`) releases
the ImGui GL resources.

## What you can edit

Each component the selected entity carries gets its own collapsible section. The curated editors
cover the engine's built-in components:

| Component | Editable fields |
| --- | --- |
| `Transform3D` | position, rotation (shown as Euler degrees), scale |
| `Camera3D` | position, target, up, FOV (degrees), near, far |
| `Material3D` | Blinn-Phong colours + shininess, or PBR metallic/roughness/emissive when **Use PBR** is ticked |
| `DirectionalLight` | direction, colour, intensity |
| `PointLight` | colour, intensity, range |
| `SpotLight` | colour, intensity, direction, inner/outer cone angles (degrees), range |
| `MeshHandle` | mesh id (read-only — assigned in code) |
| `Transform2D`, `Camera2D`, `Sprite` | the original 2D editors |

> **Rotation note:** quaternions are awkward to edit by hand, so `Transform3D` rotation is exposed
> as Euler degrees (pitch X, yaw Y, roll Z). The displayed value is cached per selection so small
> successive drags don't drift through repeated quaternion↔Euler round-trips.

### Adding, removing, and destroying

- **Add Component** — pick a type from the drop-down and press **+**. The 3D components above all
  have sensible defaults. `MeshHandle` is intentionally not offered because it needs a real mesh
  id, which can only be assigned in code.
- **Remove** — each section has a remove button.
- **Destroy Entity** / **New Entity** — at the bottom of the inspector and the entity list.

All mutations are deferred until after the ImGui draw pass to avoid invalidating the world's
iterators mid-frame.

## Saving scenes

If you pass a `ComponentRegistry` to the constructor, the **Save Scene** row writes the world out
through `SceneSaver`:

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
using var inspector = new ImGuiInspector(window, world, registry);
```

Only components with a registered serializer are persisted. The built-in 3D components do not yet
ship serializers, so a 3D scene saved this way captures entities and tags but not their 3D
component data — edit-and-save round-tripping is currently a 2D feature. Without a registry the
Save row is disabled and the inspector is edit-only.
