# Browser Demo

Proves Yaeger's WebAssembly/browser target end-to-end: a Blazor WASM host boots the engine,
renders through the WebGL 2.0 `Yaeger.Browser` backend, and reads live keyboard/mouse/touch
input — no native Silk.NET dependency anywhere in the sample.

A single paddle-and-ball toy: catch the ball with the paddle and it bounces back up (with a bit
of spin from where you hit it); miss it and a fresh ball serves from the top. There's no score —
this is bounce practice, not a scored game.

## How to run

```bash
dotnet run --project Samples/BrowserDemo/BrowserDemo.csproj
```

Then open the printed `http://localhost:...` URL in a browser. Any modern desktop or mobile
browser with WebGL 2.0 support works.

## Controls

| Input | Action |
|-------|--------|
| ← / → or A / D | Move the paddle |
| Mouse (hold LMB) or touch (drag) | Move the paddle directly under the pointer |

## What to notice

- **`Yaeger.Browser`** — `BrowserRenderSurface`, `BrowserInputState`, and `BrowserTimeSource`
  implement the same `Yaeger.Core` platform interfaces (`IRenderSurface`, `IInputState`,
  `ITimeSource`) that the native Silk.NET runtime implements, so `GameController`'s ECS/gameplay
  code (`PaddleControlSystem`, `BallMovementSystem`) is unaware it's running in a browser.
- **`PaddleControlSystem`** — reads `IInputState.IsKeyPressed` for keyboard control and
  `IsMouseButtonPressed` / `MousePositionNdc` for pointer control, so the same code path drives
  the paddle from a mouse on desktop or a finger on mobile.
- **`Game.razor`** — loads the `yaeger-browser` JS module, then hands a `DotNetObjectReference`
  to `startGameLoop` (in `wwwroot/index.html`) which pumps `requestAnimationFrame` into
  `GameController.Tick`.
