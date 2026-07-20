# Yaeger

**Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

Yaeger is a modular, experimental 2D/3D game engine written in C#. It provides a flexible and extensible platform for rapid prototyping and development of games and interactive applications, with a split between a platform-agnostic engine core and native/browser runtime integrations.

## Features

- **Entity-Component-System (ECS)** architecture, with JSON [prefabs and scenes](scenes.md)
- **2D rendering** with Silk.NET — texture-batched sprites with deterministic, layered draw ordering (`UnifiedRenderSystem`)
- **3D rendering** — mesh rendering with [lighting](lighting.md), [shadow mapping](shadows.md), and [PBR](pbr.md) materials
- **Skeletal animation** — glTF bone hierarchies and clips played via GPU skinning ([skeletal-animation.md](skeletal-animation.md))
- **Opt-in 2D camera** (pan / zoom / rotate), with an optional follow system (smoothing, deadzone, look-ahead, level bounds) — see [camera.md](camera.md)
- **Animation system** with frame-based texture cycling, sprite flipping, and a named-state-machine helper ([animation-system.md](animation-system.md))
- **Particle system** with pooled, batched emitters ([particles.md](particles.md))
- **Tilemaps** — batched, camera-culled tile grids with merged-collider physics support and Tiled (`.tmj`) import ([tilemaps.md](tilemaps.md))
- **2D physics** — spatial-hash broadphase, AABB/circle collision detection, impulse-based resolution, fixed-timestep stepping, and tunneling prevention ([physics.md](physics.md))
- **Audio system** with OpenAL support — WAV and OGG Vorbis (streamed or fully decoded), master/music/SFX volume groups ([audio-system.md](audio-system.md))
- **Text rendering** via HarfBuzz/Skia
- **UI system** — ECS-based screen-space panels, buttons, and labels ([ui.md](ui.md))
- **Input handling** (keyboard, mouse, gamepad — native only for now)
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
src/Engine/
├── Yaeger.Core/      # Platform-agnostic engine assembly — NO Silk.NET dependency.
│                     # Compiles the core logic: ECS (entities, components, queries),
│                     # transforms, physics, prefabs & scenes, and the platform-independent
│                     # systems (animation, particles, parallax), plus the platform-abstraction
│                     # interfaces in Platform/ (render/text surfaces, input state, time source,
│                     # asset resolver) and FontHandle.
│
├── Yaeger/           # Native runtime assembly — references Yaeger.Core and adds the
│                     # Silk.NET/OpenGL/OpenAL pieces: windowing, 2D + 3D rendering (sprites,
│                     # text, meshes, shadows, skybox), audio, input bindings, font runtime
│                     # (HarfBuzz/Skia), UI + editor overlay, and model loaders.
│
└── Yaeger.Browser/   # Browser/WebAssembly runtime adapters (WebGL 2.0 surface, browser input/time)
```

> **Where the code physically lives.** The platform-agnostic sources sit on disk under
> `src/Engine/Yaeger/` (e.g. `Yaeger/ECS`, `Yaeger/Graphics`, `Yaeger/Physics`) but are linked into
> **`Yaeger.Core`** via `<Compile Include>` globs in `Yaeger.Core.csproj`; `Yaeger.csproj` then
> `<Compile Remove>`s them and references `Yaeger.Core`. So the folder a file sits in does not always
> match the assembly it compiles into — the `ECS`/`Graphics`/`Physics` folders compile into
> `Yaeger.Core`, while `Text.cs`, `PhysicsDebugRenderer.cs`, `Rendering/`, `Audio/`, `Windowing/`,
> `Font/`, `UI/`, and `Inspector/` compile into `Yaeger`.

```
Samples/
├── Pong/                    # Classic Pong game
├── Platformer/              # Full 2D platformer level — the platformer-support epic's integration proof
├── BouncingBalls/           # Physics demo
├── Animation2D/             # Sprite-sheet animation demo
├── CameraDemo/              # Opt-in 2D camera demo
├── MouseDemo/               # Mouse input demo
├── ParticleDemo/            # Particle effects demo (fire, smoke, explosions)
├── SceneDemo/               # JSON scene loading demo
├── CornellBox/              # 3D Cornell Box + F1 editor overlay demo
├── Sponza/                  # glTF Sponza scene rendered through the PBR path
├── DamagedHelmet/           # glTF DamagedHelmet model with skybox, lights, and orbit camera
├── SkinnedMeshDemo/         # glTF skeletal animation (GPU skinning) demo
├── UiDemo/                  # UI system demo (menu + HUD with buttons, panels, labels)
├── BrowserDemo/             # Blazor/WebAssembly interactive paddle-and-ball demo
├── RenderingStressTest/     # Renderer stress test
└── TextRenderingExample/    # Text rendering demo
```

## License

This project is licensed under the MIT License.
