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
- **`Game.razor`** — calls `YaegerBrowser.InitializeAsync(Nav.BaseUri)` to import the
  `yaeger-browser.js` module that `Yaeger.Browser` ships as a static web asset
  (`_content/Yaeger.Browser/yaeger-browser.js`, no copy needed), then
  `YaegerBrowser.StartGameLoop(controller.Tick)` pumps `requestAnimationFrame` into
  `GameController.Tick`.

## Keyboard defaults

The browser's default key behaviour (arrows/Space scrolling, Tab moving focus, F5 reloading) is left
alone unless you opt in. Call `BrowserInputState.SetPreventDefaultKeys([Keys.Space, Keys.Up, ...])` to
suppress it for specific keys; pass an empty list to restore defaults.

## Hosting under a sub-path (GitHub Pages)

The demo resolves the packaged `yaeger-browser.js` (and relative texture paths) against the app's `<base href>`,
so it works from `/` or from a sub-path such as a GitHub Pages project site
(`https://user.github.io/repo/`). To host it under a sub-path:

1. Publish: `dotnet publish Samples/BrowserDemo -c Release -o pub`, and deploy `pub/wwwroot`.
2. In the deployed `index.html`, change `<base href="/" />` to `<base href="/repo/" />`.
3. Delete the precompressed `index.html.br` and `index.html.gz` — they still contain the old
   `<base href>` and would be served instead of your edited file.
4. Add an empty `.nojekyll` file at the site root so GitHub Pages' Jekyll doesn't drop the
   `_framework/` folder (Jekyll ignores paths starting with an underscore).

## Text

The paddle scene draws a live `Time` label with a `Text` entity through `UnifiedRenderSystem` and
`BrowserTextRenderSurface`. Glyphs are rasterized by the browser (Canvas 2D `fillText`) into cached
atlas pages at `size × devicePixelRatio`, so any CSS font works. A font handle's id is a CSS family:
use `"sans-serif"` etc. directly, or register a file first with
`await BrowserTextRenderSurface.LoadFontAsync("MyFont", "fonts/my.woff2")` and use
`new FontHandle("MyFont")`. Layout (advance, newlines, word wrap, alignment) is `TextLayout` in
`Yaeger.Core`; set `BrowserTextRenderSurface.Options` for wrap width/alignment.
