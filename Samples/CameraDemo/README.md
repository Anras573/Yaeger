# Camera Demo

Demonstrates the `Camera2D` ECS component: pan, zoom, and rotate a world-space camera while a screen-space HUD stays pinned.

## How to Run

```bash
dotnet run --project Samples/CameraDemo/CameraDemo.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Controls

| Key | Action |
|-----|--------|
| `W` / `A` / `S` / `D` | Pan camera |
| `Q` / `E`             | Zoom out / in |
| `←` / `→`             | Rotate camera |
| `R`                   | Reset camera |
| `ESC`                 | Exit |

## What to notice

- **Sprites move with the camera** — the 3×3 grid is in world coordinates, and `Renderer` applies the camera's view-projection before drawing.
- **HUD text does not** — `TextRenderer` is screen-space; the position/zoom/rotation readout stays pinned to the top-left regardless of where the camera moves.
- **Zoom scales the pan speed** — pan distance divides by zoom, so movement feels consistent when zoomed in vs out.
- **Resize the window** — the aspect ratio updates live; the grid keeps its proportions because `RenderSystem` reads `window.Size` each frame.
