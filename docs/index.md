# Yaeger

**Y**et **A**nother **E**xperimental **G**ame **E**ngine **R**epository

Yaeger is a modular, experimental 2D/3D game engine written in C#. It provides a flexible and extensible platform for rapid prototyping and development of games and interactive applications, with a split between a platform-agnostic engine core and native/browser runtime integrations.

## Features

- **Entity-Component-System (ECS)** architecture, with JSON [prefabs and scenes](scenes.md)
- **Entity hierarchy** — `Parent` + `TransformHierarchySystem` compose child transforms with their ancestors', in both 2D and 3D ([hierarchy.md](hierarchy.md))
- **2D rendering** with Silk.NET — texture-batched sprites with deterministic, layered draw ordering (`UnifiedRenderSystem`)
- **3D rendering** — mesh rendering with [lighting](lighting.md), [directional and point-light shadow mapping](shadows.md), [instanced rendering](instancing.md) for repeated meshes, [distance fog](fog.md), and [PBR](pbr.md) materials with skybox-driven [image-based lighting](pbr.md#image-based-lighting)
- **Sky** — a six-image `Skybox`, or a shader-computed `ProceduralSky` (sun/moon discs, a rotating star field, drifting clouds, no assets) — see [sky.md](sky.md)
- **Day/night cycle** — one `TimeOfDay` component driving the sun/moon direction, light colour, scene ambient, and exposure ([day-night.md](day-night.md))
- **Post-processing** — render-to-texture `PostProcessStack` chaining full-screen effects (vignette/colour-grade, bloom) over the 2D or 3D pipeline ([post-processing.md](post-processing.md))
- **Skeletal animation** — glTF bone hierarchies and clips played via GPU skinning ([skeletal-animation.md](skeletal-animation.md))
- **3D path following** — constant-speed waypoint locomotion with gradual turn-to-face, loop/ping-pong/once modes ([path-follow.md](path-follow.md))
- **Opt-in 2D camera** (pan / zoom / rotate), with an optional follow system (smoothing, deadzone, look-ahead, level bounds) — see [camera.md](camera.md)
- **3D camera rig** — a shared free-fly controller, a `Transform3D`/hierarchy bridge so a camera can be parented, target-tracking, and trauma-based shake — see [camera.md](camera.md)
- **Animation system** with frame-based texture cycling, sprite flipping, and a named-state-machine helper ([animation-system.md](animation-system.md))
- **Particle system** with pooled, batched emitters ([particles.md](particles.md))
- **Tweening** — data-driven animation of transform, light, and material fields with easing, looping, and ping-pong ([tweening.md](tweening.md))
- **Sequencing** — ordered/parallel timed steps (waits, tween/clip starters, predicates, callbacks) with pause/resume/stop/skip-to-end, for directed cutscene-style beats ([sequencing.md](sequencing.md))
- **Tilemaps** — batched, camera-culled tile grids with merged-collider physics support and Tiled (`.tmj`) import ([tilemaps.md](tilemaps.md))
- **2D physics** — spatial-hash broadphase, AABB/circle collision detection, impulse-based resolution, fixed-timestep stepping, and tunneling prevention ([physics.md](physics.md))
- **3D queries** — `World.Raycast`/`RaycastAll` against `Aabb3D` bounds, with layer/mask filtering ([queries.md](queries.md))
- **Audio system** with OpenAL support — WAV and OGG Vorbis (streamed or fully decoded), master/music/SFX volume groups ([audio-system.md](audio-system.md))
- **Text rendering** via HarfBuzz/Skia
- **UI system** — ECS-based screen-space panels, buttons, and labels ([ui.md](ui.md))
- **Input handling** (keyboard, mouse, gamepad — native only for now)
- **Editor overlay** — in-game ImGui inspector for live entity/component editing ([editor.md](editor.md))
- **Asset hot-reload** — opt-in, dev-time watcher that re-uploads changed textures and re-instantiates changed scenes without restarting ([asset-hot-reload.md](asset-hot-reload.md))
- Extensible component and system design

## Where to see it

Each feature doc ends with a **See it in action** section; this is the same map in one place.
FeatureGallery scene names are also listed in [its README](../Samples/FeatureGallery/README.md).

| Feature | Where | `dotnet run …` |
|---|---|---|
| ECS | [Pong](../Samples/Pong) — the minimal end-to-end game | `--project Samples/Pong/Pong.csproj` |
| Prefabs & scenes | [Platformer](../Samples/Platformer) `Scenes/background.json` via `SceneLoader` | `--project Samples/Platformer/Platformer.csproj` |
| Entity hierarchy | [Hierarchy](../Samples/FeatureGallery/Scenes/Hierarchy) gallery scene | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene Hierarchy` |
| 2D rendering | [Platformer](../Samples/Platformer); [Pong](../Samples/Pong) | `--project Samples/Platformer/Platformer.csproj` |
| 3D lighting | [Cornell Box](../Samples/FeatureGallery/Scenes/CornellBox), [Damaged Helmet](../Samples/FeatureGallery/Scenes/DamagedHelmet) gallery scenes; [SponzaNight](../Samples/SponzaNight) for `LightFlicker` | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Cornell Box"` |
| Shadows | [Cornell Box](../Samples/FeatureGallery/Scenes/CornellBox) (directional); [SponzaNight](../Samples/SponzaNight) (moving sun + point lights) | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Cornell Box"` |
| Instancing | [Benchmarks](../Samples/Benchmarks) `instancing` / `crowd` | `--project Samples/Benchmarks -- instancing` |
| Fog | [SponzaNight](../Samples/SponzaNight) | `--project Samples/SponzaNight/SponzaNight.csproj` |
| PBR / IBL | [Sponza](../Samples/FeatureGallery/Scenes/Sponza) (minimal PBR), [Damaged Helmet](../Samples/FeatureGallery/Scenes/DamagedHelmet) (IBL) gallery scenes | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Damaged Helmet"` |
| Sky | [SponzaNight](../Samples/SponzaNight) (`ProceduralSky`); [Damaged Helmet](../Samples/FeatureGallery/Scenes/DamagedHelmet) (cubemap `Skybox`) | `--project Samples/SponzaNight/SponzaNight.csproj` |
| Day/night cycle | [SponzaNight](../Samples/SponzaNight) — ←/→ scrub, Space freezes | `--project Samples/SponzaNight/SponzaNight.csproj` |
| Post-processing | [Post-Processing](../Samples/FeatureGallery/Scenes/PostProcessing) gallery scene; [SponzaNight](../Samples/SponzaNight) (HDR bloom) | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Post-Processing"` |
| Skeletal animation | [Skinned Mesh](../Samples/FeatureGallery/Scenes/SkinnedMesh) gallery scene; [SponzaNight](../Samples/SponzaNight) knight; [Benchmarks](../Samples/Benchmarks) `crowd` | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Skinned Mesh"` |
| 3D path following | [SponzaNight](../Samples/SponzaNight) — the patrolling knight | `--project Samples/SponzaNight/SponzaNight.csproj` |
| 2D camera | [Platformer](../Samples/Platformer) — follow + bounds; **C** for the manual debug camera | `--project Samples/Platformer/Platformer.csproj` |
| 3D camera rig | `FreeFlyCameraSystem` in the [Cornell Box](../Samples/FeatureGallery/Scenes/CornellBox)/Sponza/Skinned Mesh scenes; target tracking and shake not in a sample yet — `tests/Yaeger.Tests/Systems/CameraRigSystemTests.cs` | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Cornell Box"` |
| 2D animation | [Platformer](../Samples/Platformer) — player state machine + plain NPC `Animation` | `--project Samples/Platformer/Platformer.csproj` |
| Particles | 2D: [Platformer](../Samples/Platformer/Systems/ParticleEffectsSystem.cs); 3D: [Cornell Box](../Samples/FeatureGallery/Scenes/CornellBox), [SponzaNight](../Samples/SponzaNight) fire | `--project Samples/Platformer/Platformer.csproj` |
| Tweening | [Tween](../Samples/FeatureGallery/Scenes/Tween) gallery scene | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Tween"` |
| Sequencing | [Sequence](../Samples/FeatureGallery/Scenes/Sequence) gallery scene | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Sequence"` |
| Tilemaps | [Platformer](../Samples/Platformer) (code-built); Tiled `.tmj` import not in a sample yet — `tests/Yaeger.Tests/ECS/TiledMapLoaderTests.cs` | `--project Samples/Platformer/Platformer.csproj` |
| 2D physics | [Platformer](../Samples/Platformer) (character controller, platforms); [Bouncing Balls](../Samples/FeatureGallery/Scenes/BouncingBalls) gallery scene (impulse resolution) | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Bouncing Balls"` |
| 3D queries | Not in a sample yet — `tests/Yaeger.Tests/Physics/WorldRaycastExtensionsTests.cs` | — |
| Audio | [Platformer](../Samples/Platformer) (streamed music, pooled one-shots); [Damaged Helmet](../Samples/FeatureGallery/Scenes/DamagedHelmet) (`AudioSource3D`) | `--project Samples/Platformer/Platformer.csproj` |
| Text rendering | [Text Rendering](../Samples/FeatureGallery/Scenes/TextRendering) gallery scene | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Text Rendering"` |
| UI | [Platformer](../Samples/Platformer/Systems/GameFlowSystem.cs) (title/pause/HUD); the FeatureGallery menu itself | `--project Samples/Platformer/Platformer.csproj` |
| Input | Mouse: [Mouse](../Samples/FeatureGallery/Scenes/Mouse) gallery scene; keyboard + gamepad: [Platformer](../Samples/Platformer); touch: [BrowserDemo](../Samples/BrowserDemo) | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Mouse"` |
| Editor overlay | **F1** in the [Cornell Box](../Samples/FeatureGallery/Scenes/CornellBox), Sponza and Damaged Helmet scenes, and [SponzaNight](../Samples/SponzaNight) | `--project Samples/FeatureGallery/FeatureGallery.csproj -- --scene "Cornell Box"` |
| Asset hot-reload | Not in a sample yet — `tests/Yaeger.Tests/Assets/HotReload/` | — |
| Browser / WebAssembly | [BrowserDemo](../Samples/BrowserDemo) | `--project Samples/BrowserDemo/BrowserDemo.csproj` |

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
├── Pong/            # Minimal hello-world: a complete game in one Program.cs
├── Platformer/      # 2D showcase — physics, tilemap, camera, animation, particles, UI, audio, scenes
├── SponzaNight/     # 3D showcase — day/night, procedural sky, fog, fire, shadows, skinned knight, HDR bloom
├── FeatureGallery/  # One window, a menu of small single-feature scenes (2D and 3D); --scene <Name> to jump in
├── Benchmarks/      # Perf smoke tests (sprites, instancing, crowd), one clean process each
└── BrowserDemo/     # Blazor/WebAssembly host on the WebGL 2.0 backend
```

## License

This project is licensed under the MIT License.
