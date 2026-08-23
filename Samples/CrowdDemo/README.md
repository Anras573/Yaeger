# Crowd Demo

A perf demo for Yaeger's instanced skinning path (issue #198): spawns a grid of KhronosGroup
CesiumMan characters, all sharing one `SkeletonHandle` but each playing the walk clip out of phase,
and prints FPS plus the real GL draw-call count once per second.

## Purpose

`MeshRenderSystem` groups skinned entities by `(MeshHandle, Material3D, SkeletonHandle)` each frame,
same as it groups static entities by `(MeshHandle, Material3D)`. A group at or above
`MeshRenderSystem.InstancingThreshold` (default 4) draws with `Renderer3D.DrawInstancedSkinned` —
one or a handful of `glDrawElementsInstanced` calls, each instance's bone palette read from a texture
buffer instead of the fixed 128-matrix UBO the immediate skinned draw path uses — instead of one
skinned draw call per character. The shadow depth pre-pass instances the same way via
`ShadowMapRenderer.DrawInstancedSkinned`.

Use this sample to:
- See the draw-call count stay low as the crowd size grows, instead of scaling with character count.
- Compare FPS/draw-calls with instancing forced off (see Controls) to see the difference.

See `docs/instancing.md#instanced-skinning` for how the bone-palette texture buffer works.

## How to Run

```bash
dotnet run --project Samples/CrowdDemo/CrowdDemo.csproj
```

The CesiumMan glTF is fetched automatically on first build (skipped in CI). Requires native
libassimp at runtime (e.g. `apt install libassimp-dev` on Linux) and a display —
`System.PlatformNotSupportedException` in headless environments is expected.

## Controls

- **I** — toggle instancing on/off (off forces `InstancingThreshold` to `int.MaxValue`, so every
  character falls back to one immediate skinned draw call each)
- **ESC** — exit

## Tuning

`gridX`/`gridZ`/`spacing` are `const`s at the top of `Program.cs`. Bump the grid dimensions to raise
the crowd size and see where the non-instanced path (press I) starts to drop frames while the
instanced path stays flat.
