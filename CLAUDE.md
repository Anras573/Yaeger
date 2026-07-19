# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Restore dependencies and tools
dotnet restore yaeger.sln
dotnet tool restore

# Build the full solution
dotnet build yaeger.sln

# Run all tests
dotnet test

# Run a single test (by name filter)
dotnet test --filter "FullyQualifiedName~WorldTests.CreateEntity"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Check formatting (runs in CI — must pass before merge)
dotnet csharpier check .

# Fix formatting
dotnet csharpier format .
```

> **Formatter**: The project uses **CSharpier** (`dotnet csharpier`), not `dotnet format`. The Husky pre-commit hook runs `csharpier format` on staged `.cs` files automatically. CI enforces `csharpier check`.

> **SDK**: Requires **.NET 10.0**. Running `dotnet --version` and seeing `< 10.x` will cause build failures (`NETSDK1045`).

Running samples requires a display; `System.PlatformNotSupportedException` in headless environments is expected.

---

## Architecture

Yaeger is a modular 2D/3D game engine built around ECS. Games wire it together at the `Program.cs` level (see `Samples/Pong/` for 2D, `Samples/CornellBox/` and `Samples/Sponza/` for 3D). Per-feature documentation lives in `docs/` (start at `docs/index.md`).

### Assembly split

Three engine assemblies under `src/Engine/`:

- **`Yaeger.Core`** — platform-agnostic, NO Silk.NET dependency: ECS, components, physics, prefabs & scenes, platform-independent systems (animation, particles, parallax), and the platform-abstraction interfaces in `Platform/` (render/text surfaces, input state, time source, asset resolver).
- **`Yaeger`** — native runtime, references `Yaeger.Core` and adds Silk.NET/OpenGL/OpenAL: windowing, 2D + 3D rendering, audio, input bindings, font runtime (HarfBuzz/Skia), UI, editor overlay, and model loaders.
- **`Yaeger.Browser`** — browser/WebAssembly runtime adapters (WebGL 2.0 surface, browser input/time).

> **Where the code physically lives**: most platform-agnostic sources sit on disk under `src/Engine/Yaeger/` (e.g. `Yaeger/ECS`, `Yaeger/Graphics`, `Yaeger/Physics`) but are linked into **`Yaeger.Core`** via `<Compile Include>` globs in `Yaeger.Core.csproj`; `Yaeger.csproj` then `<Compile Remove>`s them and references `Yaeger.Core`. So the folder a file sits in does not always match the assembly it compiles into — check `Yaeger.Core.csproj` before assuming.

### ECS (`ECS/`)

- **`World`** — the central container. Holds all entities and typed `ComponentStorage<T>` instances (one per component type, created lazily).
- **`Entity`** — a value-type ID wrapper (`int`). Entities can optionally be named with a string tag.
- **`ComponentStorage<T>`** — internal dictionary-backed store per component type. Never access it directly; use `World` APIs.
- **`WorldExtensions`** — provides `Query<T1,T2>()` / `Query<T1,T2,T3>()` / `Query<T1,T2,T3,T4>()` LINQ-style extension methods. These iterate `T1`'s store and probe the remaining stores with `TryGet` — put the rarest component type first for best performance.
- **`IComponentSerializer` / `ComponentRegistry` / `PrefabLoader` / `Prefab`** — the prefab pipeline. JSON files with a `"components": [{"type": "...", ...}]` structure are loaded via `PrefabLoader`, which looks up serializers in a `ComponentRegistry`. Call `registry.RegisterEngineComponents()` to register the built-in serializers (2D and 3D component types, including lights and tilemaps — see `ECS/Serializers/`), then add game-specific ones. Instantiate via `world.Instantiate(prefab, optionalTag)`.
- **`SceneLoader` / `Scene` / `SceneSaver`** — the scene pipeline. JSON files with a top-level `"entities"` array, each entry optionally carrying a `"tag"` plus its own `"components"` array. Loader reuses the same `ComponentRegistry` as prefabs. Instantiate via `world.Instantiate(scene)`, which returns `IReadOnlyList<Entity>`. Save a world back out with `new SceneSaver(registry).Save(world, path)`. See `docs/scenes.md` and `Samples/SceneDemo`.
- **`TiledMapLoader`** — imports a level authored in Tiled's JSON export format (`.tmj`) into a `Scene` (does not go through `ComponentRegistry`; it builds `Tilemap`/`Transform2D`/`RenderLayer` components directly). Tile layers become one entity per layer; objects whose Tiled `class`/`type` matches a key in a caller-supplied `IReadOnlyDictionary<string, Prefab>` spawn that prefab at the object's position. Supports a single embedded tileset, orthogonal finite maps, and uncompressed layer data only — see `docs/tilemaps.md`.

**Critical constraint**: All components must be `struct`, never `class`. `ComponentStorage<T>` has a `where T : struct` constraint.

### Systems pattern

Systems implement `IUpdateSystem` (a single `void Update(float deltaTime)` method); render-side systems expose a `Render()` method (some via `IRenderSystem`). There is no base class — systems hold a `World` reference and call `world.Query<...>()` to iterate matching entities. The game loop calls update systems manually in order from `OnUpdate` and render systems from `OnRender` (see the samples' `Program.cs`).

### 2D rendering (`Rendering/`, `Systems/`)

- **`Renderer`** — sprite renderer. Internally batches submitted quads by texture (up to 1 000 quads per flush) so a scene sharing a texture collapses into one draw call per frame. `SubmitQuad(...)` enqueues; `EndFrame()` flushes. CPU-side vertex transforms (no `uTransform` uniform), so per-quad UV sub-regions are packed into the vertex buffer — this is how sprite-sheet animation frames work through the same batched path.
- **`UnifiedRenderSystem`** — the preferred 2D render system: draws sprites, sprite sheets, tilemaps, and text in a **deterministic order** sorted by `RenderLayer`, then `Entity.Id`, then command kind. Takes `IRenderSurface` / `ITextRenderSurface` abstractions (so it is unit-testable via fake surfaces) plus an optional `Window` for camera support — the `Window` dependency is why it compiles into `Yaeger`, not `Yaeger.Core`.
- **`RenderSystem` / `TextRenderSystem`** — the older, simpler systems consuming `Sprite`/`Text` + `Transform2D`. With `RenderSystem`, draw order follows texture-group order, not entity insertion order — use `UnifiedRenderSystem` + `RenderLayer` when layering matters.
- **`TextRenderer` / `FontTexture` / `FontManager`** — text rendering using SkiaSharp/HarfBuzz for glyph rasterisation into an atlas (bitmap or SDF — see `Font/` and `TextRenderMode`).
- **`Shader`** — wraps vertex+fragment GLSL source; shaders are compiled inline (string literals) rather than loaded from files.

### 3D rendering (`Rendering/Renderer3D`, `Systems/MeshRenderSystem`)

- **`Renderer3D`** — forward renderer for 3D meshes with depth testing and back-face culling, independent of the 2D `Renderer`. One directional light + up to 16 `PointLight`s + 8 `SpotLight`s. Shading is Blinn-Phong by default or PBR metallic/roughness when `Material3D.UsePbr` is set (see `docs/lighting.md`, `docs/pbr.md`). Supports directional shadow mapping (`SetShadowMap`) and GPU skinning via a 128-bone matrix UBO (`SetBoneMatrices`).
- **`MeshRenderSystem`** — queries `MeshHandle` + `Transform3D` + `Material3D` entities and issues draws through `Renderer3D`; renders through the first `Camera3D` entity, frustum-culls entities carrying `Aabb3D`, collects light components, and routes entities with a `BonePalette` through the skinning path. Optional constructor args wire in a `SkyboxRenderer`/`CubemapRegistry` (renders any `Skybox` entity) and a `ShadowMapRenderer` (depth pre-pass; configured by `ShadowSettings`, see `docs/shadows.md`).
- **Registries** map CPU data to GPU resources and hand back value-type ECS handle components: `GpuMeshRegistry` (`MeshData` → `MeshHandle`), `CubemapRegistry` (6 face paths → `Skybox`), `TextureManager` (path-keyed texture cache), `SkeletonRegistry` (skeleton + clips → `SkeletonHandle`).

### Asset loading (`Assets/`)

`AssimpLoader.LoadScene(path)` loads glTF/FBX/etc. via AssimpNet into a `ModelScene` (meshes, materials, and — for skinned models — a `Skeleton` plus `AnimationClip`s). `ObjLoader`/`MtlLoader` are a hand-rolled OBJ/MTL path. `Material3D.FromModel(...)` converts loaded materials.

### Skeletal animation (`Graphics/`, `Systems/SkeletalAnimationSystem`)

CPU-sampled, GPU-skinned. Entities carry `SkeletonHandle` + `AnimationPlayer` (clip name — `null` holds the bind pose — plus time, speed, loop); `SkeletalAnimationSystem.Update(dt)` samples the clip, resolves the bone hierarchy, and writes a `BonePalette` component that `MeshRenderSystem` uploads. See `docs/skeletal-animation.md` and `Samples/SkinnedMeshDemo`.

### Physics (`Physics/`)

**`PhysicsWorld2D`** is the public façade. Construct it with a `World` and optional gravity vector, then call `physicsWorld.Update(deltaTime)` each frame. `Update` runs a fixed-timestep accumulator (default 120 Hz, configurable via `fixedTimeStep`/`maxSubSteps`): callers keep passing their own frame delta, and the world internally sub-steps by whole `FixedTimeStep` increments — carrying over any remainder (exposed as `InterpolationAlpha`, in `[0, 1)`, for render-side interpolation) and capping the sub-steps per call at `MaxSubSteps` (a spiral-of-death guard: a huge stall discards the backlog instead of bursting through it). This is what makes simulation results independent of frame rate. A `deltaTime` of zero or less bypasses the accumulator and runs exactly one step immediately (useful for tests and manual single-step invocations). See `docs/physics.md`. Each step internally runs four subsystems in order:

1. `GravitySystem` — applies `Gravity` to `RigidBody2D` + `Velocity2D`
2. `MovementSystem` — integrates velocity into `Transform2D`. A dynamic entity that also carries a `BoxCollider2D` has its planned displacement swept (`SweptAabb`, box-vs-box only) against solid, non-trigger, non-one-way `BoxCollider2D` obstacles that aren't themselves dynamic (static/kinematic bodies, including tilemap-generated colliders), and clamped to just past the earliest contact if the naive displacement would tunnel through one — see `docs/physics.md`.
3. `CollisionDetectionSystem` — spatial-hash broadphase, then AABB vs AABB and circle vs circle narrowphase, stores `CollisionManifold` list. Before narrowphase, candidate pairs are filtered by each collider's `Layer`/`CollidesWith` bitmask (a symmetric check — both sides must include the other's layer); pairs that fail the check are skipped entirely (cheap early-out).
4. `CollisionResolutionSystem` — impulse-based resolution using `PhysicsMaterial` (restitution, friction). Manifolds where either collider has `IsTrigger` set are skipped here (no impulse, no positional correction) but still appear in `Manifolds`/`OnCollision` — use triggers for coins, checkpoints, goal flags, and other sensors that should be detected without physically resolving. Manifolds involving a one-way `BoxCollider2D` (`OneWay = true`) are likewise skipped unless the other body approaches from the platform's `SurfaceDirection` side (default up) while not moving against it — see below.

Components: `BoxCollider2D`, `CircleCollider2D` (both carry `Layer` (int, [0, 31]), `CollidesWith` (bitmask, defaults to "every layer"), and `IsTrigger` (bool)), `RigidBody2D` (`Dynamic`/`Static` body type), `Velocity2D`, `PhysicsMaterial`. `BoxCollider2D` additionally carries `OneWay` (bool) and `SurfaceDirection` (unit `Vector2`, default up) for one-way ("jump-through") platforms.

A one-way platform resolves a contact only when both hold: the positional-correction push on the other body aligns with `SurfaceDirection` (i.e. contact is on the solid side, not the underside), and the other body's velocity relative to the platform isn't moving against `SurfaceDirection` (i.e. not still rising up through it). Either failing means the contact is skipped — reported via `Manifolds`/`OnCollision` like a trigger, but not resolved. Call `physicsWorld.DropThrough(entity, duration)` to make an entity ignore one-way contacts for a window (the down+jump escape hatch); it has no effect on non-one-way colliders.

Subscribe to `physicsWorld.OnCollision` for collision events. Read `physicsWorld.Manifolds` for the results of the last step — check `CollisionManifold.IsTrigger` to distinguish sensor overlaps from physically-resolved collisions.

`PhysicsWorld2D` also tracks contact pairs across steps (keyed order-independently on the two entity IDs) to derive `OnCollisionEnter` (fires once when a pair starts overlapping) and `OnCollisionExit` (fires once when a pair stops — including when either entity is destroyed between steps). `OnCollision` remains the per-step "stay" signal, firing every step a pair is in contact (including the entering step). Use `OnCollisionEnter`/`OnCollisionExit` for once-per-contact gameplay reactions (stomping an enemy, coin pickup); use `OnCollision` for continuous effects (damage-over-time while standing in lava).

`BoxCollider2D`/`CircleCollider2D` are registered with `ComponentRegistry.RegisterEngineComponents()` (type ids `"BoxCollider2D"`/`"CircleCollider2D"`), so they load from prefab/scene JSON like any other component.

**Tilemap collision**: `PhysicsWorld2D.Update` runs a `TilemapColliderSystem` pass before the four subsystems above. It reads `Tilemap` + `Transform2D` entities, treats tiles whose index is in the `Tileset`'s `solidTileIndices` as solid (`Tileset.IsSolid`), and merges adjacent solid tiles into the fewest axis-aligned rectangles via `TilemapColliderMerger` (a standalone greedy-merge algorithm) — generating one static `BoxCollider2D` entity per rectangle instead of one per tile. This avoids both broadphase bloat and the tile-seam snag where a per-tile collider setup can hand `CollisionDetectionSystem.TestBoxBox` a spurious X-axis normal from an internal edge. Colliders are diffed and rebuilt whenever a tilemap's tiles change (e.g. breakable blocks via `SetTile`), and cleaned up when the tilemap entity is destroyed. See `docs/tilemaps.md`.

**Kinematic character controller**: `CharacterController2D` (box shape) + `CharacterControllerSystem` is a move-and-slide alternative to the impulse pipeline above, for player characters where bouncing, restitution, and soft positional correction are the wrong feel. Each step it integrates its own gravity (`Gravity * GravityScale`, independent of `PhysicsWorld2D.Gravity` — tune jump arcs without touching impulse-resolved bodies), then resolves movement axis-separated (X first, then Y) against solid `BoxCollider2D`/tilemap-generated obstacles: depenetrating fully and zeroing velocity into a contact instead of bouncing off it. Which axis resolves a given overlap is decided by a min-penetration-axis gate (mirroring `CollisionDetectionSystem.TestBoxBox`'s normal selection) so a body spawned embedded in a floor gets pushed up, not sideways, and running across a seam between adjacent/merged colliders never snags. A small contact skin tolerance (`ContactSkin`) lets a contact resolved to exactly zero overlap keep reporting `IsTouchingWallLeft/Right`/`IsGrounded`/`IsTouchingCeiling` on later frames — without it, a wall contact with velocity zeroed (nothing regenerating overlap, unlike gravity against the ground) would silently lose its flag one frame after impact. Respects layers/masks and one-way platforms via the same `OneWayPlatformFilter` helper `CollisionResolutionSystem` uses, and honours `StepHeight` for auto-climbing short ledges (checked ahead of the axis gate, since a climbable ledge's vertical overlap is often smaller than its horizontal). Don't also attach a `BoxCollider2D`/`CircleCollider2D` to a controller entity — it would be redundantly processed by the impulse pipeline this component bypasses.

**Moving platforms and rider carrying**: a kinematic `BoxCollider2D` entity (its own `Velocity2D` integrated by `MovementSystem`, ignored by gravity and impulse resolution) acts as a moving platform. `CharacterControllerSystem` carries a grounded controller along with it: before anything else each step, if `CharacterController2D.GroundEntity` (the entity last landed on, written by the system) has moved since the previous step, the controller's position shifts by that same displacement first — tracked via a per-entity position snapshot the system keeps internally, refreshed at the end of every `Update` call. This is a no-op for a stationary ground, so it's always safe to apply. Because the carry runs before this step's own move-and-slide resolution, riding into a wall depenetrates normally instead of being pushed through it, and a vertically moving platform doesn't cause grounded-state flicker (the rider descends with it instead of momentarily falling behind and re-landing every step). Call `CharacterControllerSystem.Update` *after* whatever moves the platform (typically `PhysicsWorld2D.Update`) in the same frame. `PlatformPath` (waypoints, speed, ping-pong or loop) + `PlatformPathSystem` is an optional helper that drives a kinematic platform's `Velocity2D` back and forth or around a loop, so samples don't have to hand-roll patrol movement — games can drive kinematic velocity themselves instead.

### Graphics components (`Graphics/`)

Value-type ECS components — 2D: `Sprite`, `Transform2D`, `Camera2D`, `Color`, `SpriteSheet`, `Animation`, `AnimationState`, `Text`, `RenderLayer`, `ParallaxLayer`, `Tilemap` (+ `Tileset`); 3D: `Transform3D`, `Camera3D`, `Material3D`, `MeshHandle`, `Aabb3D`, `DirectionalLight`, `PointLight`, `SpotLight`, `Skybox`, `SkeletonHandle`, `AnimationPlayer`, `BonePalette`.

`SpriteSheet` calculates normalised UV rectangles per frame index via `GetFrameUv(int frameIndex)`.

`Tilemap` + `Transform2D` renders a grid of tiles from a `Tileset` texture through `UnifiedRenderSystem` — batched by the shared texture (typically one draw call per map, subject to the renderer's 1 000-quad batch limit), camera-culled, `RenderLayer`-ordered. Tile indices are row-major with row 0 at the **top**; the transform position is the map's **bottom-left corner**; `Tilemap.EmptyTile` (`-1`) marks empty cells. See `docs/tilemaps.md`.

`Camera2D` is a `record struct` with `Position`, `Zoom`, `Rotation`. Attach it to an entity and construct `RenderSystem`/`UnifiedRenderSystem` with a `Window` to activate camera-aware rendering. Without a `Window`, or without a `Camera2D` entity, the renderer falls back to identity view (NDC-direct — the pre-camera behaviour). `TextRenderer` is explicitly screen-space and does NOT honour the camera. See `docs/camera.md` and `Samples/CameraDemo`.

### Particles (`Graphics/ParticleEmitter` + `Systems/ParticleSystem`)

`ParticleEmitter` (paired with a `Transform2D` for position) configures continuous emission: rate, lifetime, direction/spread, speed, and start→end colour/size lerps. `ParticleSystem` implements `IUpdateSystem`; call `Update(dt)` from the update loop (simulation: age, recycle, emit) and `Render()` from the render callback **after** the main render system (it submits quads through the renderer's batched path and flushes). Each emitter owns a fixed-size `ParticlePool` of `Particle` structs recycled in-place — no per-frame heap allocation. See `docs/particles.md` and `Samples/ParticleDemo`.

### UI (`UI/`, `Systems/UiSystem` + `Systems/UiRenderSystem`)

Retained-mode, screen-space UI built from ECS components: `UiRect` (position/size in pixels) plus `UiPanel` (background colour, border radius), `UiButton` (normal/hovered/pressed colours), and `UiLabel` (text, colour, font size). `UiBuilder` is a convenience factory (`CreatePanel`/`CreateButton`/`CreateLabel`, fraction-of-window helpers). `UiSystem.Update(dt)` (update loop) mouse-hit-tests buttons and writes `UiButtonState` (`IsHovered`/`IsPressed`/`WasClicked` — poll `WasClicked` for click handling); `UiRenderSystem.Render()` (render callback, after game rendering) draws quads via `UiRenderer` and labels via the text renderer. UI ignores `Camera2D`. See `docs/ui.md` and `Samples/UiDemo`.

### Editor overlay (`Inspector/`)

`ImGuiInspector` is an in-game Dear ImGui overlay for live entity/component editing (2D and 3D). Construct with `(window, world, optionalRegistry)`, call `inspector.Render(delta)` **last** in `OnRender` every frame (even while hidden), and bind a key to `Toggle()`. Selected entities get in-world **selection gizmos** (transform axes, AABB/camera/light visualisations) rendered by `GizmoRenderer`; appearance is configurable via `inspector.GizmoStyle`, visibility via `inspector.ShowGizmos`. See `docs/editor.md` and `Samples/CornellBox` (F1 toggles).

### Windowing (`Windowing/Window`)

Wraps Silk.NET's `IWindow`. The public surface is event-based: `OnLoad`, `OnUpdate`, `OnRender`, `OnResize`, `OnClosing`. Exposes `window.Gl` (the OpenGL context) and `window.AudioContext`. Always `using var window = Window.Create();` to ensure disposal.

### Input (`Input/`)

Two static classes initialised by `Window`:

- **`Keyboard`** — `IsKeyPressed(Keys)` for polling, `AddKeyDown` / `AddKeyUp` for events. `Keys` enum is a curated subset; extend it plus `KeyMapper` when new keys are needed.
- **`Mouse`** — `IsButtonPressed(MouseButton)`, `AddButtonDown` / `AddButtonUp`, plus `Position` (client pixels), `PositionNdc` (OpenGL NDC), `PositionDelta`, `ScrollDelta`, `AddScroll`. World-space mouse is the caller's job via an inverse `Camera2D.ViewProjection`.

### Audio (`Audio/`)

`AudioContext` wraps OpenAL via Silk.NET.OpenAL. `SoundBuffer.FromFile()` loads `.wav` files; `SoundSource` controls playback and looping.

---

## Test conventions

- Framework: **xUnit** — test methods are annotated with `[Fact]`.
- Naming: `MethodOrFeature_ShouldExpectedBehavior` (e.g. `CreateEntity_ShouldReturnUniqueEntity`).
- Structure: AAA (Arrange / Act / Assert) with no shared state between tests.
- Test helper components are private structs inside the test class.
- Anything needing a live OpenGL/audio context (GPU rendering, windowing, input, audio) is not unit-tested, but the CPU-side logic around it is: mesh/vertex data, shadow-matrix math, and systems tested through the `Platform/` abstractions (e.g. `UnifiedRenderSystem` via fake `IRenderSurface`s). Follow that pattern for new systems.
