# Mouse Demo

Demonstrates the new `Mouse` input API: button state (held + events), cursor position in both client-pixel and NDC spaces, and scroll-wheel input.

## How to Run

```bash
dotnet run --project Samples/MouseDemo/MouseDemo.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Controls

| Input | Action |
|-------|--------|
| LMB (hold) | Paint sprites at cursor |
| RMB (click) | Clear all painted sprites |
| Scroll wheel | Adjust sprite size |
| ESC | Exit |

## What to notice

- **`Mouse.IsButtonPressed(MouseButton.Left)`** — polled each `Update` tick while held.
- **`Mouse.AddButtonDown(MouseButton.Right, ...)`** — event-based, fires once per press.
- **`Mouse.PositionNdc`** — raw pixel position converted to NDC using the live window size, so sprites spawn under the cursor even if you resize the window.
- **`Mouse.AddScroll(delta => ...)`** — scroll events accumulate into a `spriteScale` variable; the HUD shows the current value.
