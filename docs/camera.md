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

`UnifiedRenderSystem` picks up the camera each frame. Construct it with a `Window`:

```csharp
// Sprites with camera. Pass a TextRenderer as the second argument when you also need text.
var renderSystem = new UnifiedRenderSystem(renderer, null, world, window);
```

If you omit the `Window`, `UnifiedRenderSystem` skips camera updates and the renderer keeps whatever view-projection it was last set to (identity by default). Samples that don't need a camera (`Pong`, `BouncingBalls`, `Animation2D`, `RenderingStressTest`) pass no window and render in NDC.

If multiple `Camera2D` entities exist, the **first one** encountered during iteration wins. There's no `MainCamera` tag component yet — add one when you need deterministic multi-camera selection.

## World-space sprites vs screen-space text

`Renderer` applies the camera; `TextRenderer` does **not**. This is deliberate: in most 2D games, sprites are world objects (move with the camera) and text is UI (stays pinned). See `Samples/CameraDemo` for a direct demonstration — the HUD text stays at the top-left as the camera pans.

If you need world-anchored labels (e.g., a name floating above a sprite), position a text entity at the anchor's world coordinates and multiply through manually, or file an issue and we can add a `UseCamera` flag to `Text`.

## Following a target

`CameraFollow` + `CameraFollowSystem` track a target entity's `Transform2D` automatically —
smoothing, a deadzone, and velocity-based look-ahead — instead of you writing that logic by hand.

```csharp
var cameraFollowSystem = new CameraFollowSystem(world, window); // window is optional

var cam = world.CreateEntity();
world.AddComponent(cam, new Camera2D());
world.AddComponent(cam, new CameraFollow(
    targetEntity,
    smoothing: 5f,                                // 1/s exponential rate; <= 0 snaps instantly
    deadzoneHalfExtents: new Vector2(0.5f, 0.3f),  // target can move this far before the camera reacts
    lookAheadTime: 0.15f));                        // biases toward targetEntity's Velocity2D direction

window.OnUpdate += delta => cameraFollowSystem.Update((float)delta);
```

Call `Update` after your gameplay/physics update, so it reads each entity's final position for
the frame. If the target is destroyed (or otherwise loses its `Transform2D`), the camera simply
holds its last position rather than snapping to the origin or throwing.

Look-ahead reads the target's `Velocity2D` component if present (`desiredPosition = targetPosition
+ velocity * lookAheadTime`); a target without one just doesn't get a look-ahead offset — it's
never an error.

### Clamping to level bounds

Add a `CameraBounds` to the same entity to keep the camera's visible span inside a rectangle —
the classic "don't show past the level edge" constraint:

```csharp
world.AddComponent(cam, new CameraBounds(Vector2.Zero, new Vector2(levelWidth, levelHeight)));

// Or derive it directly from a Tilemap (whose Transform2D position is its bottom-left corner):
world.AddComponent(cam, CameraBounds.FromTilemap(tilemap, tilemapTransform));
```

`CameraBounds` only has an effect when a `CameraFollow` is also present on the same entity — the
follow system reads and clamps against it after smoothing/deadzone/look-ahead are applied.
Clamping accounts for the camera's current `Zoom` and the window's aspect ratio (the visible
half-extents are `(aspectRatio / Zoom, 1 / Zoom)`, per `Camera2D`'s remarks above), so zooming in
or out shifts how close to an edge the camera can get. If the level is narrower than the
viewport on an axis (bounds smaller than the visible span), that axis is centered on the level's
own midpoint instead of clamped — there's no position that avoids showing past the edge there
anyway.

See `Samples/CameraDemo` — press Space to toggle between manual pan/zoom/rotate and follow mode,
where WASD moves a red target square that the camera tracks within the level bounds.

## Known limitations

- No camera shake yet.
- No viewport sub-regions (split-screen).
