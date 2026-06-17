# Yaeger

YAEGER - **Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

## Overview

Yaeger is a modular, experimental 2D/3D game engine written in C#. It aims to provide a flexible and extensible platform for rapid prototyping and development of games and interactive applications. The engine is designed with an Entity-Component-System (ECS) architecture and is split into a platform-agnostic abstraction layer (`Yaeger.Core`) plus a native runtime (`Yaeger`) for Silk.NET/OpenGL/OpenAL integration and a browser runtime (`Yaeger.Browser`).

## Features

- Entity-Component-System (ECS) architecture, with JSON prefabs and scenes
- 2D rendering with Silk.NET (texture-batched sprites with deterministic, layered draw ordering via `RenderLayer` and `UnifiedRenderSystem`)
- 3D rendering — mesh rendering with lighting, shadow mapping, and PBR materials (see [`docs/`](docs/index.md))
- Opt-in 2D camera (pan / zoom / rotate; world-space sprites + screen-space text)
- Frame-based animation and a pooled, batched particle system
- 2D physics (AABB/circle collision detection + impulse-based resolution)
- Audio playback via OpenAL and text rendering via HarfBuzz/Skia
- In-game ImGui editor overlay for live entity/component editing
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

Run the rendering stress test:

```bash
dotnet run --project Samples/RenderingStressTest/RenderingStressTest.csproj
```

## Testing

The project includes a comprehensive test suite covering the ECS system and graphics components. To run tests:

```bash
dotnet test
```

For more information about testing, see the [Testing Guide](docs/TESTING.md).

## Project Structure

- `src/Engine/Yaeger.Core/` - Platform-agnostic abstractions with no Silk.NET dependency (render/text surfaces, input state, time source, asset resolver, font handle)
- `src/Engine/Yaeger/` - The engine plus native runtime (ECS, components, physics, systems, 2D/3D rendering, audio, input, font, windowing)
- `src/Engine/Yaeger.Browser/` - Browser runtime adapters (Canvas2D render surface, browser input/time sources)
- `tests/Yaeger.Tests/` - Unit test suite (ECS, Graphics, Physics, Assets, Font, Rendering, Systems, Browser)
- `Samples/` - Example games and demos
  - `Pong/` - Classic Pong game implementation
  - `BouncingBalls/` - Physics demo
  - `Animation2D/` - Sprite-sheet animation demo
  - `CameraDemo/` - Opt-in 2D camera (pan / zoom / rotate)
  - `MouseDemo/` - Mouse input (paint trail + scroll resize)
  - `ParticleDemo/` - Particle effects (fire, smoke, explosions)
  - `SceneDemo/` - JSON scene loading
  - `CornellBox/` - 3D Cornell Box + F1 editor overlay
  - `Sponza/` - glTF Sponza scene rendered through the PBR path
  - `UiDemo/` - ImGui UI demo
  - `BrowserDemo/` - Blazor/WebAssembly browser loop + browser input/runtime integration
  - `RenderingStressTest/` - Renderer stress test (FPS vs sprite count)
  - `TextRenderingExample/` - Text rendering demo
- `docs/` - Documentation (see [`docs/index.md`](docs/index.md))

## Usage

`Yaeger.Core` defines the platform-agnostic abstractions (render/text surfaces, input state, time source, asset resolver) shared by the runtimes; it has no Silk.NET dependency.
For desktop/native behavior (ECS, window, rendering, input, audio), reference `Yaeger`.
For browser/WebAssembly behavior (Canvas2D rendering, browser input/frame timing), reference `Yaeger.Browser`.
See the Pong sample for a minimal end-to-end native implementation.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or improvements.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
