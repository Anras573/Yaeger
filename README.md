# Yaeger

YAEGER - **Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

## Overview

Yaeger is a modular, experimental 2D/3D game engine written in C#. It aims to provide a flexible and extensible platform for rapid prototyping and development of games and interactive applications. The engine is designed with an Entity-Component-System (ECS) architecture and is split into a platform-agnostic engine core (`Yaeger.Core`) plus a native runtime (`Yaeger`) for Silk.NET/OpenGL/OpenAL integration and a browser runtime (`Yaeger.Browser`).

## Features

- Entity-Component-System (ECS) architecture, with JSON prefabs and scenes
- 2D rendering with Silk.NET (texture-batched sprites with deterministic, layered draw ordering via `RenderLayer` and `UnifiedRenderSystem`)
- 3D rendering — mesh rendering with lighting, shadow mapping, and PBR materials (see [`docs/`](docs/index.md))
- Render-to-texture post-processing — `PostProcessStack` chaining full-screen effects (vignette/colour-grade, bloom) over the 2D or 3D pipeline (see [`docs/post-processing.md`](docs/post-processing.md))
- Skeletal animation — glTF bone hierarchies and clips played via GPU skinning
- Tilemaps — batched, camera-culled tile grids
- Opt-in 2D camera (pan / zoom / rotate; world-space sprites + screen-space text)
- Frame-based animation and a pooled, batched particle system
- 2D physics (spatial-hash broadphase, AABB/circle collision detection + impulse-based resolution)
- Audio playback via OpenAL and text rendering via HarfBuzz/Skia
- ECS-based screen-space UI (panels, buttons, labels — see [`docs/ui.md`](docs/ui.md))
- In-game ImGui editor overlay for live entity/component editing, with in-world selection gizmos
- Opt-in asset hot-reload — re-uploads changed textures and re-instantiates changed scenes without restarting (see [`docs/asset-hot-reload.md`](docs/asset-hot-reload.md))
- Input handling (keyboard, mouse; browser runtime maps single-touch/pen to mouse-style input)
- Sample games and demos (see [`Samples/`](Samples))
- Extensible component and system design
- Comprehensive unit test suite

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Compatible OS (Windows, macOS, Linux)

## Build & Run

Clone the repository:

```bash
git clone https://github.com/Anras573/Yaeger.git
cd Yaeger
```

Build the engine and sample projects:

```bash
dotnet build yaeger.sln
```

Run the Pong sample:

```bash
dotnet run --project Samples/Pong/Pong.csproj
```

Browse the single-feature demos, optionally jumping straight into one:

```bash
dotnet run --project Samples/FeatureGallery/FeatureGallery.csproj
dotnet run --project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Cornell Box"
```

Run a benchmark (perf smoke test):

```bash
dotnet run --project Samples/Benchmarks -- sprites|instancing|crowd
```

## Testing

The project includes a comprehensive test suite covering the ECS system and graphics components. To run tests:

```bash
dotnet test
```

For more information about testing, see the [Testing Guide](docs/TESTING.md).

## Project Structure

- `src/Engine/Yaeger.Core/` - Platform-agnostic engine assembly (no Silk.NET dependency): ECS, components/transforms, physics, prefabs & scenes, the platform-independent systems (animation, particles, parallax), and the platform-abstraction interfaces (render/text surfaces, input state, time source, asset resolver). The abstraction interfaces (`Platform/`) and `FontHandle` (`Graphics/`) live here under `src/Engine/Yaeger.Core/`; the ECS/Graphics/Physics/Systems sources physically live under `src/Engine/Yaeger/` and are linked into `Yaeger.Core` via `<Compile Include>` globs (`Yaeger.Core.csproj`), which `Yaeger.csproj` then removes and references.
- `src/Engine/Yaeger/` - Native runtime (references `Yaeger.Core`): windowing, 2D/3D rendering, audio, input bindings, font runtime, UI + editor overlay, and model loaders — the Silk.NET/OpenGL/OpenAL-dependent pieces
- `src/Engine/Yaeger.Browser/` - Browser runtime adapters (WebGL 2.0 render surface, browser input/time sources)
- `tests/Yaeger.Tests/` - Unit test suite (ECS, Graphics, Physics, Assets, Font, Rendering, Systems, Browser)
- `Samples/` - Example games and demos (see [`docs/index.md#where-to-see-it`](docs/index.md#where-to-see-it) for which sample shows each feature)
  - `Pong/` - Minimal hello-world: a complete game in one `Program.cs`
  - `Platformer/` - 2D showcase: physics, tilemaps, camera follow (+ a debug free camera), animation, parallax, particles, UI (title/pause/HUD), pooled one-shot audio, and scene loading — see [`Samples/Platformer/README.md`](Samples/Platformer/README.md) for a feature -> file table
  - `SponzaNight/` - 3D showcase: day/night cycle, procedural sky, fog, brazier fire and flickering lights, shadows, a patrolling skinned knight, HDR bloom
  - `FeatureGallery/` - One window, a menu of small single-feature scenes (Mouse, Text Rendering, Bouncing Balls, Cornell Box, Tween, Sequence, Skinned Mesh, Sponza, Damaged Helmet, Post-Processing) — see [`Samples/FeatureGallery/README.md`](Samples/FeatureGallery/README.md)
  - `Benchmarks/` - Perf smoke tests (sprite batching, mesh instancing, crowd/skinning instancing), one clean process per benchmark
  - `BrowserDemo/` - Blazor/WebAssembly interactive paddle-and-ball demo (keyboard, mouse & touch)
- `docs/` - Documentation (see [`docs/index.md`](docs/index.md))

## Usage

`Yaeger.Core` is the platform-agnostic engine assembly — it contains the ECS, components, physics, prefabs & scenes, and the platform-independent systems, alongside the platform-abstraction interfaces, with no Silk.NET dependency. Reference it directly for headless simulation, gameplay logic, and unit tests.
For desktop/native behavior (windowing, 2D/3D rendering, input, audio), reference `Yaeger`.
For browser/WebAssembly behavior (WebGL 2.0 rendering, browser input/frame timing), reference `Yaeger.Browser`.
See the Pong sample for a minimal end-to-end native implementation.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or improvements.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
