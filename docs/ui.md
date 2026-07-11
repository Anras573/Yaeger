# UI System

Yaeger ships a small retained-mode UI layer built from the same ECS primitives as the rest of the
engine: UI elements are ordinary entities carrying UI components, updated by `UiSystem` and drawn
by `UiRenderSystem`. Everything is **screen-space** (pixel coordinates, origin at the top-left of
the window) and deliberately ignores the `Camera2D` — a HUD stays put while the world scrolls.

See it in action in [`Samples/UiDemo`](../Samples/UiDemo): a Play/Quit main menu that switches to
a live-updating HUD.

## Components

| Component | Fields | Purpose |
| --- | --- | --- |
| `UiRect` | `Position`, `Size` (pixels) | Placement — every UI entity has one |
| `UiPanel` | `BackgroundColor`, `BorderRadius` | A solid-colour rectangle |
| `UiButton` | `Normal`, `Hovered`, `Pressed` colours | A clickable rectangle |
| `UiButtonState` | `IsHovered`, `IsPressed`, `WasClicked` | Written by `UiSystem` each frame — read, don't write |
| `UiLabel` | `Text`, `Color`, `FontSize` | Text drawn at the rect's position |

All are plain structs, so they follow the engine-wide component rules (serializable-friendly,
no behaviour). `WasClicked` is a one-frame pulse: it is `true` only on the frame the mouse button
is released over a button on which the press also started (drag-off cancels the click).

## Wiring it up

```csharp
using var window = Window.Create();
var world = new World();
var fontManager = new FontManager();

var font = fontManager.Load("Assets/Roboto-Regular.ttf");
var textRenderer = new TextRenderer(window, fontManager);
var uiRenderer = new UiRenderer(window);
var uiSystem = new UiSystem(world);
var uiRenderSystem = new UiRenderSystem(world, uiRenderer, textRenderer, font, window);

window.OnLoad += () =>
{
    var ui = new UiBuilder(world, window.Size);

    var w = ui.Width(0.25f);          // 25 % of the window width
    var h = 60f;

    ui.CreatePanel(ui.CenterX(w) - 20, 100, w + 40, 3 * h + 80, new Color(0.1f, 0.1f, 0.15f));
    ui.CreateButton(ui.CenterX(w), 140, w, h,
        normal:  new Color(0.2f, 0.4f, 0.8f),
        hovered: new Color(0.3f, 0.5f, 0.9f),
        pressed: new Color(0.15f, 0.3f, 0.6f),
        tag: "btn-play");
    ui.CreateLabel(ui.CenterX(w) + 20, 155, "Play", 28f, Color.White);
};

window.OnUpdate += delta =>
{
    uiSystem.Update((float)delta);    // hit-testing BEFORE you read button state

    if (world.TryGetEntity("btn-play", out var btn)
        && world.TryGetComponent<UiButtonState>(btn, out var state)
        && state.WasClicked)
    {
        StartGame();
    }
};

window.OnRender += _ =>
{
    // ... game render systems first ...
    uiRenderSystem.Render();          // UI last, so it draws on top
};

window.Run();
```

Ordering rules:

- **Update:** call `uiSystem.Update(dt)` before any game code that polls `UiButtonState`.
- **Render:** call `uiRenderSystem.Render()` **after** the game render systems so the UI overlays
  the scene. (If you also use the `ImGuiInspector` editor overlay, that still goes last of all.)

## UiBuilder

`UiBuilder(world, windowSize)` is an optional convenience factory: `CreatePanel`, `CreateButton`
(pre-attaches a `UiButtonState`), and `CreateLabel`, each taking pixel coordinates and an optional
entity tag. It also has window-relative helpers — `Width(fraction)` / `Height(fraction)` and
`CenterX(elementWidth)` / `CenterY(elementHeight)`.

The window size is captured when the builder is constructed, so recreate the builder (and,
typically, rebuild your layout) inside `Window.OnResize` if you want resolution-independent
placement after a resize.

You can skip the builder entirely and attach `UiRect` + `UiPanel`/`UiButton`/`UiLabel` components
by hand — the systems only care about the components.

## How it renders

`UiRenderSystem` draws in two passes: panel and button rectangles go through `UiRenderer`
(a texture-free, colour-only batched quad renderer — up to 1 000 quads per flush, no assets
required), then labels are delegated to the text renderer using the single default font passed at
construction. Buttons pick their colour from `UiButtonState` (`Pressed` > `Hovered` > `Normal`),
so hover/press feedback needs no game code.
