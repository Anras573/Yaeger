# Camera

## 2D Camera

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

### Known limitations

- No camera shake yet (the 3D camera has one — see below).
- No viewport sub-regions (split-screen).

## 3D Camera

`Camera3D` (`Position`, `Target`, `Up`, `Fov`, `Near`, `Far`) is a look-at-style camera —
`MeshRenderSystem` reads it directly every frame to build the view/projection matrices. Three
pieces build a full camera rig on top of it: a free-fly controller for development/debug flying,
a bridge that lets a camera participate in the entity hierarchy, and `LookAtTarget`/`CameraShake`
for tracking a target and reacting to impacts.

### Free-fly controller

`FreeFlyCameraSystem` is the WASD + right-mouse-drag fly camera every 3D sample used to hand-roll
separately (`Samples/SkinnedMeshDemo`, `Samples/Sponza`, and `Samples/CornellBox` each shipped a
near-identical 70-odd-line `FreeFlySystem` — now gone, replaced by this one shared controller):

```csharp
var freeFlySystem = new FreeFlyCameraSystem(world, cameraEntity, moveSpeed: 10f);

Keyboard.AddKeyDown(Keys.Escape, window.Close);
window.OnUpdate += deltaTime => freeFlySystem.Update((float)deltaTime);
```

Hold the right mouse button and move the mouse to look around; W/A/S/D moves forward/strafe, E/Q
rises/falls. `moveSpeed` (world units/second) and `lookSensitivity` are constructor parameters —
`CornellBox` uses `moveSpeed: 3f` for its small room, `Sponza`/`SkinnedMeshDemo` use the `10f`
default-sized scale. It writes straight to `Camera3D.Position`/`Target` and never touches
`Transform3D` — a free-fly camera has no parent, so there's nothing to bridge.

### The `Transform3D` bridge — and why a camera couldn't be parented before

`MeshRenderSystem` reads `Camera3D.Position`/`Target`/`Up` directly and has never consulted
`Transform3D`. That's a real gap: `TransformHierarchySystem` will happily compose a camera
entity's `Transform3D` from a `Parent` + `LocalTransform3D`, but the renderer silently ignored the
result — a camera riding inside a moving lift, or mounted on a vehicle, did nothing.

`CameraRigSystem` closes that gap: when a `Camera3D` entity also carries a `Transform3D`, that
transform becomes authoritative every step. `Position` copies straight across; `Target`/`Up` are
derived by rotating a fixed local forward/up (`-Z`/`+Y` — the same look-down-negative-Z convention
`Yaeger.Audio.AudioSpatialMath` and glTF cameras use) by `Transform3D.Rotation`:

```csharp
var lift = world.CreateEntity("lift");
world.AddComponent(lift, new Transform3D(Vector3.Zero, Quaternion.Identity, Vector3.One));

var camera = world.CreateEntity("camera");
world.AddComponent(camera, new Camera3D { Fov = MathF.PI / 4f, Near = 0.1f, Far = 100f });
world.AddComponent(camera, new Parent(lift));
world.AddComponent(camera, new LocalTransform3D(new Vector3(0f, 1.5f, 3f), Quaternion.Identity, Vector3.One));

var hierarchySystem = new TransformHierarchySystem(world);
var cameraRigSystem = new CameraRigSystem(world);

// Drive the lift however gameplay normally would...
world.AddComponent(lift, new Transform3D(new Vector3(0f, liftHeight, 0f), Quaternion.Identity, Vector3.One));

// ...resolve the hierarchy, then the camera rig, before rendering.
hierarchySystem.Update(deltaTime);
cameraRigSystem.Update(deltaTime);
meshRenderSystem.Render();
```

A `Camera3D` with **no** `Transform3D` is untouched by this bridge — every existing sample
authors `Camera3D` directly with no `Transform3D`, so this is fully backward compatible; only a
camera that opts in by also carrying a `Transform3D` gets driven by it.

### `LookAtTarget`: tracking an entity

Attach alongside `Camera3D` to smoothly aim `Target` at another entity's `Transform3D` position —
the same framerate-independent exponential smoothing `CameraFollow` uses on the 2D side:

```csharp
world.AddComponent(camera, new LookAtTarget(playerEntity, smoothing: 5f)); // <= 0 snaps instantly
```

`LookAtTarget` only ever adjusts `Target`; whatever positions the camera (free-fly, the
`Transform3D` bridge, or direct authoring) is unaffected. If the target entity is destroyed or
otherwise loses its `Transform3D`, `Target` simply holds its last value rather than snapping to the
origin or throwing — the same contract `CameraFollowSystem` has for a missing `Transform2D`.

### `CameraShake`: trauma-based impact shake

Attach alongside `Camera3D` and add trauma on a hit, explosion, or landing:

```csharp
world.AddComponent(camera, new CameraShake(decay: 1.5f, maxOffset: 0.3f, seed: 1));

// On impact:
var shake = world.GetComponent<CameraShake>(camera);
world.AddComponent(camera, shake.AddTrauma(0.6f));
```

`Trauma` (0–1) decays back to zero at `Decay` units/second; the jitter applied to
`Position`/`Target` scales with **`Trauma²`**, not `Trauma` linearly, so a small knock barely
shakes while a big one shakes hard (the standard trauma-shake formulation — Squirrel Eiserloh, GDC
2016). The jitter itself comes from a deterministic pseudo-noise function of `Seed` and elapsed
shake time rather than `System.Random`, so the same seed and elapsed time always produce the same
offset — that's what makes it unit-testable as pure math despite looking randomized.

Shake is applied **additively on top of the resolved camera transform**, and — critically —
**never permanently accumulates**: `CameraRigSystem` subtracts exactly the offset it added last
step before doing anything else each step, and only re-adds a freshly computed offset at the very
end. That's why **`CameraRigSystem.Update` must run after anything else that positions the
camera** (`FreeFlyCameraSystem`, gameplay code, `TransformHierarchySystem`) — same convention as
`CameraFollowSystem` — so a relative move like free-fly's `Position += displacement` always reads
a clean, unshaken base rather than baking in a residual jitter that compounds frame after frame.

### Update order

```csharp
// 1. Anything that positions the camera directly: gameplay code, FreeFlyCameraSystem,
//    or TransformHierarchySystem resolving a Parent chain.
freeFlySystem.Update(deltaTime);      // or: hierarchySystem.Update(deltaTime);

// 2. The camera rig: Transform3D bridge, then LookAtTarget smoothing, then CameraShake — always last.
cameraRigSystem.Update(deltaTime);

// 3. Render.
meshRenderSystem.Render();
```

### Out of scope (for now)

- **Cinematic/keyframed camera paths** — animate `Camera3D.Position`/`Target` directly with
  `Tween`'s `Camera3DPosition`/`Camera3DTarget` channels (see [tweening.md](tweening.md)); this
  rig doesn't have its own path/keyframe system.
- **Hard cuts between cameras** — drive which `Camera3D` entity is "active" yourself, or sequence
  it with `SequenceSystem` (see [sequencing.md](sequencing.md)).
- **Orbit/turntable and first-person controllers** — only the fly camera is provided so far.

See `Samples/SkinnedMeshDemo`, `Samples/Sponza`, and `Samples/CornellBox` for `FreeFlyCameraSystem`
in use.
