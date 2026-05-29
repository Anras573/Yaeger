# Yaeger

YAEGER - **Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

## Overview

Yaeger is a modular, experimental 2D game engine written in C#. It aims to provide a flexible and extensible platform for rapid prototyping and development of games and interactive applications. The engine is designed with an Entity-Component-System (ECS) architecture and is split into a platform-agnostic core (`Yaeger.Core`) plus a native runtime (`Yaeger`) for Silk.NET/OpenGL/OpenAL integration.

## Features

- Entity-Component-System (ECS) architecture
- 2D rendering with Silk.NET (texture-batched sprites with deterministic ordering)
- Deterministic layered draw ordering via `RenderLayer` and `UnifiedRenderSystem`
- Opt-in 2D camera (pan / zoom / rotate; world-space sprites + screen-space text)
- Input handling (keyboard, mouse; browser runtime maps single-touch/pen to mouse-style input)
- Sample games (see `Samples/Pong`, `Samples/BouncingBalls`, `Samples/Animation2D`, `Samples/CameraDemo`)
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

For more information about testing, see the [Testing Guide](docs/testing.md).

## Project Structure

- `src/Engine/Yaeger.Core/` - Platform-agnostic engine core (ECS, scenes, animation, transforms, physics, gameplay logic)
- `src/Engine/Yaeger/` - Native runtime (windowing, rendering, input bindings, audio, font runtime)
- `src/Engine/Yaeger.Browser/` - Browser runtime adapters (Canvas2D render surface, browser input/time sources)
- `tests/Yaeger.Tests/` - Unit test suite
  - `ECS/` - Tests for ECS components
  - `Graphics/` - Tests for graphics primitives
- `Samples/` - Example games and demos
  - `BrowserDemo/` - Blazor/WebAssembly browser loop + browser input/runtime integration
  - `Pong/` - Classic Pong game implementation
  - `BouncingBalls/` - Physics demo
  - `Animation2D/` - Sprite-sheet animation demo
  - `CameraDemo/` - Opt-in 2D camera (pan / zoom / rotate)
  - `MouseDemo/` - Mouse input (paint trail + scroll resize)
  - `SceneDemo/` - JSON scene loading
  - `RenderingStressTest/` - Renderer stress test (FPS vs sprite count)
- `docs/` - Documentation
  - `TESTING.md` - Comprehensive testing guide

## Usage

For headless/platform-agnostic simulation and gameplay logic, reference `Yaeger.Core`.
For desktop/native runtime behavior (window, rendering, input, audio), reference `Yaeger`.
For browser/WebAssembly behavior (Canvas2D rendering, browser input/frame timing), reference `Yaeger.Browser`.
See the Pong sample for a minimal end-to-end native implementation.

## Contributing

Contributions are welcome! Please open issues or submit pull requests for bug fixes, new features, or improvements.

## License

This project is licensed under the MIT License. See `LICENSE` for details.
