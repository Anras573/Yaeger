# Mesh Instancing Demo

A perf demo for Yaeger's instanced 3D rendering path (issue #148): spawns a large grid of boxes
sharing one of two `Material3D`s and prints FPS plus the real GL draw-call count once per second.

## Purpose

`MeshRenderSystem` groups non-skinned entities by `(MeshHandle, Material3D)` each frame. A group at
or above `MeshRenderSystem.InstancingThreshold` (default 4) is drawn with a single
`glDrawElementsInstanced` call via `Renderer3D.DrawInstanced` instead of one `glDrawElements` call
per entity; the shadow depth pre-pass groups the same way, but by mesh alone (depth doesn't depend
on material), so same-mesh casters collapse into one draw call even across different materials.

Use this sample to:
- See the draw-call count stay flat (2 for the main pass, 1 for the shadow pass) as the instance
  count grows, instead of scaling with entity count.
- Compare FPS/draw-calls with instancing forced off (see Controls) to see what #148 fixed.

## How to Run

```bash
dotnet run --project Samples/MeshInstancingDemo/MeshInstancingDemo.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is
expected.

## Controls

- **I** — toggle instancing on/off (off forces `InstancingThreshold` to `int.MaxValue`, so every
  entity falls back to one draw call each — the pre-#148 behaviour)
- **ESC** — exit

## Tuning

`gridX`/`gridY`/`gridZ`/`spacing` are `const`s at the top of `Program.cs`. Bump the grid dimensions
to raise the instance count and see where the non-instanced path (press I) starts to drop frames
while the instanced path stays flat.
