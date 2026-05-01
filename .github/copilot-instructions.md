# Yaeger - Copilot Instructions

## Repository Overview

Yaeger is a modular, experimental 2D game engine written in C# using Entity-Component-System (ECS) architecture. The engine leverages Silk.NET for graphics, input handling, and windowing capabilities.

**Key Technologies:**
- **Language**: C# with .NET 10.0 target framework
- **Graphics**: Silk.NET for OpenGL rendering
- **Audio**: Silk.NET.OpenAL for audio playback
- **Text**: SkiaSharp + HarfBuzzSharp for glyph rasterisation
- **Image Processing**: StbImageSharp
- **Architecture**: Entity-Component-System (ECS) pattern
- **Platform**: Cross-platform (Windows, macOS, Linux)

## Build Instructions

### Prerequisites
- **.NET 10.0 SDK** — the project will NOT build with .NET 9.0 or earlier

### Build Commands
```bash
dotnet restore yaeger.sln
dotnet tool restore
dotnet build yaeger.sln
```

### Running Samples
```bash
dotnet run --project Samples/Pong/Pong.csproj
```

Samples require a display. `System.PlatformNotSupportedException` in headless environments is expected.

### Formatting
The project uses **CSharpier** — not `dotnet format`.

```bash
# Fix formatting
dotnet csharpier format .

# Verify (what CI runs)
dotnet csharpier check .
```

A Husky pre-commit hook automatically runs `csharpier format` on staged `.cs` files.

### Testing
```bash
# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~WorldTests.CreateEntity"
```

The test suite uses xUnit and covers ECS, physics, and graphics primitives. Rendering, windowing, input, and audio require a live platform context and are not unit-tested.

## Architecture

### ECS (`src/Engine/Yaeger/ECS/`)
- **`World`** — central container; holds all entities and one `ComponentStorage<T>` per component type (created lazily).
- **`Entity`** — value-type (`struct`) ID wrapper. Optionally registered under a string tag.
- **`WorldExtensions`** — `Query<T1,T2>()` / `Query<T1,T2,T3>()` / `Query<T1,T2,T3,T4>()` extension methods. Iterates `T1`'s store and probes the rest — put the rarest type first.
- **`ComponentStorage<T>`** — internal store per component type. Access only through `World` APIs, never directly.
- **`ComponentRegistry` / `PrefabLoader` / `Prefab`** — JSON prefab pipeline. Files use `{"components": [{"type": "...", ...}]}`. Call `registry.RegisterEngineComponents()` then `world.Instantiate(prefab, optionalTag)`.
- **`SceneLoader` / `Scene`** — JSON multi-entity scene pipeline. Files use `{"entities": [{"tag": "...", "components": [...]}, ...]}`. Reuses the prefab registry. `world.Instantiate(scene)` returns `IReadOnlyList<Entity>`. Load-only today.

**All components must be `struct`, never `class`.**

### Systems Pattern
Systems implement `IUpdateSystem` (`void Update(float deltaTime)`). They hold a `World` reference and call `world.Query<...>()`. The game loop calls them manually in order.

### Rendering (`src/Engine/Yaeger/Rendering/`)
- **`Renderer`** — batches submitted quads by texture (up to 1 000 per flush). Use `SubmitQuad(...)` between `BeginFrame()` and `EndFrame()`. CPU-side vertex transforms; per-quad UV sub-regions are supported on the batched path.
- **`TextRenderer` / `FontManager`** — text rendering via SkiaSharp glyph atlas.
- **`RenderSystem` / `TextRenderSystem`** — ECS systems consuming `Sprite`/`Text` + `Transform2D`.

### Physics (`src/Engine/Yaeger/Physics/`)
**`PhysicsWorld2D`** is the public façade. Call `physicsWorld.Update(deltaTime)` each frame. Internal pipeline: Gravity → Movement → Collision Detection → Collision Resolution. Subscribe to `physicsWorld.OnCollision` for events.

Components: `BoxCollider2D`, `CircleCollider2D`, `RigidBody2D`, `Velocity2D`, `PhysicsMaterial`.

### Graphics Components (`src/Engine/Yaeger/Graphics/`)
Value-type ECS components: `Sprite`, `Transform2D`, `Camera2D`, `Color`, `SpriteSheet`, `Animation`, `AnimationState`, `Text`.

`Camera2D` is opt-in. Attach one to an entity and pass `Window` as the third arg to `RenderSystem(...)` to activate camera-aware rendering; without either, the renderer uses identity view (NDC-direct). `TextRenderer` stays screen-space regardless.

### Windowing (`src/Engine/Yaeger/Windowing/`)
Event-based: `OnLoad`, `OnUpdate`, `OnRender`, `OnResize`, `OnClosing`. Always `using var window = Window.Create();`.

### Input (`src/Engine/Yaeger/Input/`)
Two static classes initialised by the `Window`:
- `Keyboard` — `IsKeyPressed(Keys)`, `AddKeyDown`/`AddKeyUp`. Extend `Keys` + `KeyMapper` together when new keys are needed.
- `Mouse` — button state, `Position` (pixels) + `PositionNdc` (-1..1), `ScrollDelta`, and an `AddScroll` event.

### Audio (`src/Engine/Yaeger/Audio/`)
`SoundBuffer.FromFile()` loads `.wav` files; `SoundSource` controls playback and looping.

## Project Structure

- `src/Engine/Yaeger/` — core engine library
- `tests/Yaeger.Tests/` — xUnit test suite
- `Samples/` — runnable example games and demos (one subdirectory per sample)
- `docs/` — documentation (animation, audio, physics, testing guides)

## Development Workflow

1. Ensure .NET 10.0 is available: `dotnet --version`
2. `dotnet build yaeger.sln` to verify current state
3. `dotnet test` after changes
4. Formatting is enforced by the pre-commit hook and CI — no manual step needed if hooks are installed
