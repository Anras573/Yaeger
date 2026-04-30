# 2D Camera

Yaeger's `Camera2D` is an opt-in ECS component. When no `Camera2D` entity exists in the `World`, the engine renders in NDC directly (the pre-camera default) and existing samples work unchanged.

## Adding a camera

```csharp
var cam = world.CreateEntity();
world.AddComponent(cam, new Camera2D { Zoom = 2f });
```

`Camera2D` has three fields:

| Field | Type | Default | Meaning |
|-------|------|---------|---------|
| `Position` | `Vector2` | `(0, 0)` | Camera centre in world space |
| `Zoom` | `float` | `1` | `>1` narrows the visible span (things appear larger) |
| `Rotation` | `float` | `0` | Radians; positive = camera rotates counter-clockwise |

At `Zoom = 1` with window aspect ratio `A`, the visible world span is `[-A, A] × [-1, 1]`.

## Who applies it

`RenderSystem` picks up the camera each frame. Construct it with a `Window`:

```csharp
var renderSystem = new RenderSystem(renderer, world, window);
```

If you omit the `Window`, `RenderSystem` skips camera updates and the renderer keeps whatever view-projection it was last set to (identity by default). Samples that don't need a camera (`Pong`, `BouncingBalls`, `Animation2D`, `RenderingStressTest`) pass no window and render in NDC.

If multiple `Camera2D` entities exist, the **first one** encountered during iteration wins. There's no `MainCamera` tag component yet — add one when you need deterministic multi-camera selection.

## World-space sprites vs screen-space text

`Renderer` applies the camera; `TextRenderer` does **not**. This is deliberate: in most 2D games, sprites are world objects (move with the camera) and text is UI (stays pinned). See `Samples/CameraDemo` for a direct demonstration — the HUD text stays at the top-left as the camera pans.

If you need world-anchored labels (e.g., a name floating above a sprite), position a text entity at the anchor's world coordinates and multiply through manually, or file an issue and we can add a `UseCamera` flag to `Text`.

## Known limitations

- No depth sorting — draw order is texture-group order, as before.
- No camera shake / follow systems yet — they'd be separate systems that mutate `Camera2D.Position` before `RenderSystem.Render()` runs.
- No viewport sub-regions (split-screen).
