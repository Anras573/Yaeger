# Yaeger

**Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

Yaeger is a modular, experimental 2D/3D game engine written in C#. It provides a flexible and extensible platform for rapid prototyping and development of games and interactive applications, with a split between platform-agnostic abstractions and native/browser runtime integrations.

## Features

- **Entity-Component-System (ECS)** architecture, with JSON [prefabs and scenes](scenes.md)
- **2D rendering** with Silk.NET — texture-batched sprites with deterministic, layered draw ordering (`UnifiedRenderSystem`)
- **3D rendering** — mesh rendering with [lighting](lighting.md), [shadow mapping](shadows.md), and [PBR](pbr.md) materials
- **Opt-in 2D camera** (pan / zoom / rotate) — see [camera.md](camera.md)
- **Animation system** with frame-based texture cycling ([animation-system.md](animation-system.md))
- **Particle system** with pooled, batched emitters ([particles.md](particles.md))
- **2D physics** — AABB/circle collision detection and impulse-based resolution
- **Audio system** with OpenAL support ([audio-system.md](audio-system.md))
- **Text rendering** via HarfBuzz/Skia
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
src/Engine/Yaeger.Core/        # Platform-agnostic abstractions (no Silk.NET dependency)
                               # render/text surfaces, input state, time source, asset resolver, font handle

src/Engine/Yaeger/             # The engine + native (Silk.NET/OpenGL/OpenAL) runtime
├── ECS/                       # Entities, components, queries, prefabs, scenes
├── Graphics/                  # Value-type components (Transform2D/3D, Sprite, Camera2D/3D, lights, …)
├── Physics/                   # AABB/circle collision detection + impulse resolution
├── Systems/                   # Update/render systems (animation, particles, mesh, UI, …)
├── Rendering/                 # OpenGL 2D + 3D renderers (sprites, text, meshes, shadows, skybox)
├── Audio/                     # OpenAL audio runtime
├── Input/                     # Silk.NET input bindings
├── Font/                      # HarfBuzz/Skia text runtime
├── UI/ + Inspector/           # ImGui-based UI widgets and the editor overlay
└── Windowing/                 # Window and context management

src/Engine/Yaeger.Browser/     # Browser/WebAssembly runtime adapters (Canvas2D surface, browser input/time)

Samples/
├── Pong/                    # Classic Pong game
├── BouncingBalls/           # Physics demo
├── Animation2D/             # Sprite-sheet animation demo
├── CameraDemo/              # Opt-in 2D camera demo
├── MouseDemo/               # Mouse input demo
├── ParticleDemo/            # Particle effects demo (fire, smoke, explosions)
├── SceneDemo/               # JSON scene loading demo
├── CornellBox/              # 3D Cornell Box + F1 editor overlay demo
├── Sponza/                  # glTF Sponza scene rendered through the PBR path
├── UiDemo/                  # ImGui UI demo
├── BrowserDemo/             # Blazor/WebAssembly browser loop demo
├── RenderingStressTest/     # Renderer stress test
└── TextRenderingExample/    # Text rendering demo
```

## License

This project is licensed under the MIT License.
