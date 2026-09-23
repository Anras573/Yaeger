# Feature Gallery

One window, one menu, many small single-feature demo scenes. Pick a scene from the menu to run
it; press `ESC` to return to the menu from any scene, and `ESC` again from the menu to exit.

## How to Run

```bash
dotnet run --project Samples/FeatureGallery/FeatureGallery.csproj
```

Pass `--scene <Name>` to launch straight into a scene, skipping the menu:

```bash
dotnet run --project Samples/FeatureGallery/FeatureGallery.csproj -- --scene Mouse
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is
expected.

**Note:** The Skinned Mesh, Sponza, and Damaged Helmet scenes load glTF models via AssimpLoader and
require native libassimp at runtime (e.g. `apt install libassimp-dev` on Linux). Their model assets
are fetched automatically on first build (skipped in CI) — see `Assets.targets`.

## Scenes

| Scene | `--scene` name | Controls |
|-------|----------------|----------|
| Mouse | `Mouse` | LMB (hold): paint sprites at the cursor · RMB: clear · Scroll wheel: resize the painted sprite |
| Text Rendering | `Text Rendering` | None — a static showcase of font loading, the glyph atlas, and SDF text rendering across mixed sizes |
| Bouncing Balls | `Bouncing Balls` | `SPACE`: toggle the collider-wireframe debug overlay |
| Cornell Box | `Cornell Box` | WASD move, Q/E up/down, RMB-drag look, `F1`: toggle inspector |
| Tween | `Tween` | None — a static camera watching data-driven tweens play out |
| Sequence | `Sequence` | `SPACE`: skip to end · `R`: restart |
| Skinned Mesh | `Skinned Mesh` | WASD move, Q/E up/down, RMB-drag look |
| Sponza | `Sponza` | WASD move, Q/E up/down, RMB-drag look, `F1`: toggle inspector |
| Damaged Helmet | `Damaged Helmet` | LMB-drag: orbit · scroll: zoom · `SPACE`: pause auto-orbit · `F1`: toggle inspector |
| Post-Processing | `Post-Processing` | `B`: bloom · `V`: vignette · `T`: tone-map operator · `P`: toggle stack |
| Hierarchy | `Hierarchy` | `1`/`2`/`3`: select sun/planet/moon · `+`/`-`: adjust selected body's spin speed · `O`: destroy the planet (orphans the moon) · `R`: rebuild · `F1`: toggle inspector |

Every scene also returns to the menu on `ESC`.

## Adding a scene

1. Create a folder under `Scenes/<Name>/` with a `<Name>Scene.cs` implementing `IDemoScene`
   (`Samples/FeatureGallery/IDemoScene.cs`):
   - `Name`/`Description` are shown in the menu; `Controls` is shown as a HUD overlay while the
     scene runs.
   - `Load(Window window)` creates the scene's own `World`, systems, and entities, and binds any
     input the scene needs. Each scene owns its resources — nothing is shared with other scenes.
   - `Update`/`Render` run every frame while the scene is active.
   - `Dispose()` must undo everything `Load` did: dispose GL-owning objects (renderers, font
     managers) and remove any `Keyboard`/`Mouse` bindings added in `Load` via the matching
     `Keyboard.RemoveKeyDown`/`RemoveKeyUp`/`Mouse.RemoveButtonDown`/`RemoveButtonUp`/`RemoveScroll`
     call — the static input dictionaries have no other way to unbind a closure, and a stale
     binding would otherwise leak into the next scene (or the menu).
   - **Never bind `Keys.Escape`** — `SceneHost` owns it to return to the menu.
2. Put any scene-specific assets under `Assets/<Name>/`; put assets shared across scenes (e.g.
   the default font) under `Assets/Shared/`.
3. Register the scene's factory in the `scenes` array in `Program.cs`.

`SceneHost` disposes the outgoing scene and resets GL state (depth test, face culling, blending,
bound framebuffer, viewport) before loading the next one, so a scene never has to guess what a
previous scene left enabled.
