# Yaeger

**Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

Yaeger is a modular, experimental 2D game engine written in C#. It provides a flexible and extensible platform for rapid prototyping and development of games and interactive applications, with a split between platform-agnostic core logic and native runtime integrations.

## Features

- **Entity-Component-System (ECS)** architecture
- **2D rendering** with Silk.NET
- **Batch rendering** for efficient sprite rendering
- **Animation system** with frame-based texture cycling
- **Particle system** with pooled, batched emitters
- **Audio system** with OpenAL support
- **Input handling** (keyboard, mouse)
- **Editor overlay** — in-game ImGui inspector for live entity/component editing ([editor.md](editor.md))
- Extensible component and system design

## Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- Compatible OS (Windows, macOS, Linux)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/Anras573/Yaeger.git
cd Yaeger

# Build the engine and samples
dotnet build yaeger.sln

# Run the Pong sample
dotnet run --project Samples/Pong/Pong.csproj
```

## Project Structure

```
src/Engine/Yaeger.Core/        # Platform-agnostic core (ECS/scenes/animation/transforms/physics/gameplay logic)

src/Engine/Yaeger/             # Native runtime integrations
├── Rendering/                 # OpenGL rendering systems (sprites, text)
├── Audio/                     # OpenAL audio runtime
├── Input/                     # Silk.NET input bindings
├── Font/                      # HarfBuzz/Skia text runtime
└── Windowing/                 # Window and context management

Samples/
├── Pong/                    # Classic Pong game
├── BouncingBalls/           # Physics demo
├── Animation2D/             # Sprite-sheet animation demo
├── CameraDemo/              # Opt-in 2D camera demo
├── MouseDemo/               # Mouse input demo
├── ParticleDemo/            # Particle effects demo (fire, smoke, explosions)
├── SceneDemo/               # JSON scene loading demo
├── CornellBox/              # 3D Cornell Box + F1 editor overlay demo
├── RenderingStressTest/     # Renderer stress test
└── TextRenderingExample/    # Text rendering demo
```

## License

This project is licensed under the MIT License.
