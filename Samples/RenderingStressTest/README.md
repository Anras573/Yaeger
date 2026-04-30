# Rendering Stress Test

A perf smoke-test for Yaeger's `Renderer`: spawns thousands of sprites sharing a single texture and prints the frame rate once per second.

## Purpose

The renderer batches quads by texture and flushes one draw call per texture per frame. With a single shared texture, every sprite in this sample collapses into one batched draw call — so FPS here is a proxy for CPU-side submission cost (ECS iteration + `Vector3.Transform` per vertex), not GPU fill rate.

Use this sample to:
- Verify the renderer scales to thousands of sprites without falling over.
- Spot regressions in submission/batching cost after touching `Renderer.cs` or `RenderSystem.cs`.

## How to Run

```bash
dotnet run --project Samples/RenderingStressTest/RenderingStressTest.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Controls

- **ESC** — exit

## Tuning

`spriteCount` is a `const` at the top of `Program.cs`. Bump it if 5 000 is CPU-bound on your machine and you want to see where it breaks.
