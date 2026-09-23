# Benchmarks

Perf smoke tests for Yaeger. One benchmark runs per process — a clean window, a clean `World`, no
menu UI or leftover state from another scene that could skew the numbers. Each benchmark lives
under its own folder (`Sprites/`, `Instancing/`, `Crowd/`) and implements the shared `IBenchmark`
interface (`IBenchmark.cs` — a copy of `FeatureGallery`'s `IDemoScene` shape, not a reference to it,
since benchmarks are deliberately a separate app).

## How to Run

```bash
dotnet run --project Samples/Benchmarks -- <benchmark> [count]
```

Running with no arguments prints usage and exits:

```bash
dotnet run --project Samples/Benchmarks
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is
expected.

## Benchmarks

| Arg | Measures | Default count | Controls |
|---|---|---|---|
| `sprites` | 2D sprite-batching / submission cost — thousands of sprites sharing one texture, so the renderer collapses them into one batched draw call per frame. FPS here is a proxy for CPU-side submission cost (ECS iteration + per-vertex transform), not GPU fill rate. | 5 000 sprites | **ESC** exit |
| `instancing` | 3D instanced-rendering draw-call collapse (issue #148) — a grid of boxes sharing one of two materials, drawn via `Renderer3D.DrawInstanced` instead of one draw call per entity. | 2 000 boxes (20×10×10 grid) | **I** toggle instancing on/off (forces `InstancingThreshold` to `int.MaxValue` when off), **ESC** exit |
| `crowd` | 3D instanced-skinning draw-call collapse (issue #198) — a grid of CesiumMan characters sharing one skeleton, each playing the walk clip out of phase, drawn via `Renderer3D.DrawInstancedSkinned`. | 64 characters (8×8 grid) | **I** toggle instancing on/off, **ESC** exit |

`count` is optional and overrides the benchmark's default entity count, e.g.:

```bash
dotnet run --project Samples/Benchmarks -- crowd 400
```

`instancing` and `crowd` derive a grid from `count` (preserving their original aspect ratio —
2:1:1 for `instancing`, square for `crowd`), so the actual instance count may be rounded to the
nearest grid that fits.

The `crowd` benchmark's CesiumMan glTF is fetched automatically on first build (skipped in CI, see
`Assets.targets`). Requires native libassimp at runtime (e.g. `apt install libassimp-dev` on
Linux).

## Expected output format

All three benchmarks report once per second, in the same format, via the shared `FrameStats`
helper:

```
[Sprites] FPS: 60 | avg frame: 16.62 ms | 5000 sprites
[Instancing] FPS: 60 | avg frame: 16.58 ms | draw calls: 2 main + 1 shadow | 2000 instances
[Crowd] FPS: 60 | avg frame: 16.71 ms | draw calls: 2 main + 1 shadow | 64 characters
```

`sprites` has no GPU draw-call count (2D quads aren't instanced); `instancing` and `crowd` report
`Renderer3D.DrawCallCount`/`ShadowMapRenderer.DrawCallCount` — real GL draw calls issued, where an
instanced group of any size counts as one.

## History

Ported from three now-deleted samples as part of the `Samples/` consolidation (issue #263):

- `RenderingStressTest` → `sprites`
- `MeshInstancingDemo` → `instancing`
- `CrowdDemo` → `crowd`

Each benchmark kept its original entity counts, controls, and reporting behaviour; only the
per-second console reporting was unified into the shared `FrameStats` helper, and the fixed grid
dimensions became derived from an optional count argument instead of hardcoded constants.
