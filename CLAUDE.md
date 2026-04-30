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

Yaeger is a 2D game engine library (`src/Engine/Yaeger/`) built around ECS. Games wire it together at the `Program.cs` level (see `Samples/Pong/`).

### ECS (`ECS/`)

- **`World`** — the central container. Holds all entities and typed `ComponentStorage<T>` instances (one per component type, created lazily).
- **`Entity`** — a value-type ID wrapper (`int`). Entities can optionally be named with a string tag.
- **`ComponentStorage<T>`** — internal dictionary-backed store per component type. Never access it directly; use `World` APIs.
- **`WorldExtensions`** — provides `Query<T1,T2>()` / `Query<T1,T2,T3>()` / `Query<T1,T2,T3,T4>()` LINQ-style extension methods. These iterate `T1`'s store and probe the remaining stores with `TryGet` — put the rarest component type first for best performance.
- **`IComponentSerializer` / `ComponentRegistry` / `PrefabLoader` / `Prefab`** — the prefab pipeline. JSON files with a `"components": [{"type": "...", ...}]` structure are loaded via `PrefabLoader`, which looks up serializers in a `ComponentRegistry`. Call `registry.RegisterEngineComponents()` to register the built-in serializers, then add game-specific ones. Instantiate via `world.Instantiate(prefab, optionalTag)`.

**Critical constraint**: All components must be `struct`, never `class`. `ComponentStorage<T>` has a `where T : struct` constraint.

### Systems pattern

Systems implement `IUpdateSystem` (a single `void Update(float deltaTime)` method). There is no base class — systems hold a `World` reference and call `world.Query<...>()` to iterate matching entities. The game loop calls systems manually in order (see `Samples/Pong/Program.cs`).

### Rendering (`Rendering/`)

- **`Renderer`** — sprite renderer. Internally batches submitted quads by texture (up to 1 000 quads per flush) so a scene sharing a texture collapses into one draw call per frame. `SubmitQuad(...)` enqueues; `EndFrame()` flushes. CPU-side vertex transforms (no `uTransform` uniform), so per-quad UV sub-regions are packed into the vertex buffer — this is how sprite-sheet animation frames work through the same batched path.
- **`TextRenderer` / `FontTexture` / `FontManager`** — text rendering using SkiaSharp/HarfBuzz for glyph rasterisation into an atlas.
- **`RenderSystem` / `TextRenderSystem`** — ECS systems that consume `Sprite`/`Text` + `Transform2D` components and delegate to the renderer classes.
- **`Shader`** — wraps vertex+fragment GLSL source; shaders are compiled inline (string literals) rather than loaded from files.

> Draw order follows texture-group order, not entity insertion order. There's no depth-sort system yet, so if you need specific layering, use distinct textures per layer.

### Physics (`Physics/`)

**`PhysicsWorld2D`** is the public façade. Construct it with a `World` and optional gravity vector, then call `physicsWorld.Update(deltaTime)` each frame. Internally it runs four subsystems in order:

1. `GravitySystem` — applies `Gravity` to `RigidBody2D` + `Velocity2D`
2. `MovementSystem` — integrates velocity into `Transform2D`
3. `CollisionDetectionSystem` — AABB vs AABB and circle vs circle detection, stores `CollisionManifold` list
4. `CollisionResolutionSystem` — impulse-based resolution using `PhysicsMaterial` (restitution, friction)

Components: `BoxCollider2D`, `CircleCollider2D`, `RigidBody2D` (`Dynamic`/`Static` body type), `Velocity2D`, `PhysicsMaterial`.

Subscribe to `physicsWorld.OnCollision` for collision events. Read `physicsWorld.Manifolds` for the results of the last step.

### Graphics components (`Graphics/`)

Value-type ECS components: `Sprite`, `Transform2D`, `Camera2D`, `Color`, `SpriteSheet`, `Animation`, `AnimationState`, `Text`.

`SpriteSheet` calculates normalised UV rectangles per frame index via `GetFrameUv(int frameIndex)`.

`Camera2D` is a `record struct` with `Position`, `Zoom`, `Rotation`. Attach it to an entity and construct `RenderSystem` with a `Window` (third arg) to activate camera-aware rendering. Without a `Window`, or without a `Camera2D` entity, the renderer falls back to identity view (NDC-direct — the pre-camera behaviour). `TextRenderer` is explicitly screen-space and does NOT honour the camera. See `docs/camera.md` and `Samples/CameraDemo`.

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
- Rendering, windowing, input, and audio are not unit-tested (they require a live OpenGL/audio context).
