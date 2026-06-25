# Editor Overlay

`ImGuiInspector` is an in-game [Dear ImGui](https://github.com/ocornut/imgui) overlay that lists
every entity in a `World` and lets you live-edit the components attached to the selected one. It
works for both 2D and 3D scenes, so it doubles as a lightweight 3D scene editor for the
`Renderer3D` / `MeshRenderSystem` pipeline. Edits are committed to the world when the inspector's
render pass ends; with the recommended ordering below (scene first, overlay last) that is after the
scene was drawn this frame, so the change shows up in the running game on the next frame.

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

## Seeing the selected entity in the world

Editing a light by typing numbers is hard when you can't see what they do. Whenever an entity is
selected, the inspector draws **selection gizmos** directly into the scene so you can see *where*
the entity is and *which way it faces* while you drag its values:

| Component | Gizmo |
| --- | --- |
| `Transform2D` | red/green X/Y orientation axes at the entity's position plus an amber rectangle tracing the sprite's scaled, rotated bounds |
| `Camera2D` | a yellow rectangle outlining the camera's visible viewport (derived from `Position`, `Zoom`, `Rotation`) |
| `Transform3D` | RGB orientation axes (X red, Y green, Z blue) at the entity's position |
| `Aabb3D` (meshes) | an amber wireframe box around the mesh's world-space bounds |
| `DirectionalLight` | a small "sun" with a bundle of parallel rays pointing the way the light travels |
| `PointLight` | a wireframe sphere tracing the light's `Range`, in the light's colour |
| `SpotLight` | a wireframe cone from the light along its `Direction`, opened to the outer cone angle |
| `Camera3D` | a yellow view frustum showing what the camera frames |

Lights and the camera are coloured to match, so a selected red point light shows a red range sphere.
Gizmos are drawn on top of the scene (no depth testing), so a light tucked behind a wall is still
visible while you position it.

Each gizmo is projected so it lines up exactly with the rendered scene. A selected 3D entity is
drawn through the first `Camera3D` in the world (the same camera `MeshRenderSystem` renders
through). A selected 2D entity (`Transform2D` / `Camera2D`) is drawn through the first `Camera2D`,
mirroring `RenderSystem`; if the 2D scene has no camera, gizmos fall back to NDC-direct exactly as
the 2D renderer does, so even a camera-less Pong-style scene shows them. The overlay is on by
default; untick **Show selection gizmos** at the bottom of the inspector (or set
`inspector.ShowGizmos = false`) to hide it.

> Because the gizmos read the live world every frame, dragging a `DirectionalLight`'s direction or a
> `SpotLight`'s cone angle updates the gizmo immediately — no extra wiring beyond the standard
> *scene first, overlay last* render order.

### Customising the gizmo appearance

The default colours and sizes suit a roughly unit-scaled scene, but a 0.5-unit axis is invisible in a
1000-unit world and the amber bounds may clash with a particular art style. `inspector.GizmoStyle`
exposes a runtime [`GizmoStyle`](../src/Engine/Yaeger/Inspector/GizmoStyle.cs) you can retune — every
value defaults to the look described above, so you only set what you want to change:

```csharp
// Make gizmos usable in a large-scale scene and tweak a couple of colours.
inspector.GizmoStyle.SizeMultiplier = 50f;                 // scale axes, arrows, light cores
inspector.GizmoStyle.BoundsColor = new Vector4(0, 1, 1, 1); // cyan mesh outlines
inspector.GizmoStyle.DepthTest = true;                      // hide gizmos behind geometry
inspector.GizmoStyle.LineWidth = 2f;                        // thicker lines
```

| Option(s) | Effect |
| --- | --- |
| `AxisXColor`, `AxisYColor`, `AxisZColor` | Colours of the 3D/2D orientation axes (default red / green / blue) |
| `BoundsColor` | Mesh & sprite bounds outline colour (default amber) |
| `CameraColor` | `Camera2D` viewport and `Camera3D` frustum colour (default yellow) |
| `DirectionalLightColor`, `PointLightColor`, `SpotLightColor` | Override a light gizmo's colour; leave `null` (default) to keep using the light's own colour |
| `SizeMultiplier` | Global scale for fixed-size elements (axes, directional arrows/rays/sun, point-light core). Range-driven shapes — the point-light reach sphere and the spot-light cone — keep showing the light's *actual* extent and are not scaled |
| `AxisLength`, `Axis2DLength`, `DirectionalArrowLength`, `ArrowHeadSize`, `DirectionalRaySpread`, `DirectionalSunRadius`, `PointLightCoreSize` | Per-element base sizes (each then multiplied by `SizeMultiplier`) |
| `SphereSegments`, `SunSphereSegments`, `PointLightCoreSegments`, `ConeSegments` | Tessellation of the circles/spheres/cone — raise for smoother shapes, lower to cut cost |
| `DepthTest` | `false` (default) draws gizmos on top of everything; `true` lets scene geometry occlude them |
| `LineWidth` | Gizmo line width in pixels (driver-dependent clamping applies) |

The style is runtime-only — it is not saved with scenes.

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

Only components with a registered serializer are persisted. `RegisterEngineComponents()` covers
both the 2D and 3D built-in components, so edit-and-save round-tripping works for 3D scenes too.
The one exception is `MeshHandle`: its `Id` is an opaque, runtime-assigned key into a
`GpuMeshRegistry` that is not portable across runs, so it is intentionally not serialized — a saved
scene keeps an entity's `Transform3D`, `Material3D`, lights, etc., but the mesh must be re-assigned
in code on load. Without a registry the Save row is disabled and the inspector is edit-only.
