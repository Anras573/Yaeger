# Platformer

The integration proof for the "platformer support" epic — and, per [#264](https://github.com/Anras573/Yaeger/issues/264),
the flagship sample that absorbed every remaining single-feature 2D demo (`Animation2D`,
`CameraDemo`, `ParticleDemo`, `SceneDemo`, `UiDemo`, `OneShotAudioDemo`). One complete, playable
level exercising every feature both epics added, composed the way a real game would in
`Program.cs` (wiring only — behaviour lives in `Systems/`/`Components/`).

## How to Run

```bash
dotnet run --project Samples/Platformer/Platformer.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Feature -> file

| Feature | File |
|---|---|
| Title screen, pause menu, HUD (coins/lives) | [`Systems/GameFlowSystem.cs`](Systems/GameFlowSystem.cs) — built with `UiBuilder`/`UiSystem`/`UiRenderSystem`, replacing the old `Text`/`TextRenderer` HUD |
| Particles (run dust, landing puff, coin sparkle) | [`Systems/ParticleEffectsSystem.cs`](Systems/ParticleEffectsSystem.cs) |
| Pooled one-shot audio (jump/coin/stomp) | `Program.cs` — `AudioSystem.PlayOneShot`, replacing one dedicated `SoundSource` per sound |
| Scene-loaded background decorations | [`Scenes/background.json`](Scenes/background.json), loaded via `SceneLoader` in `Program.cs` |
| Debug free camera (pan/zoom/rotate) | [`Systems/DebugCameraSystem.cs`](Systems/DebugCameraSystem.cs) |
| Plain, non-state-machine `Animation` | `Scenes/background.json`'s decorative idling NPC, contrasted with the player's `AnimationStateMachine` in `Program.cs` |
| Player movement, physics, tilemap, platforms | `Program.cs` — `CharacterController2D`, code-built `Tilemap`, one-way + moving platforms |
| Coin/enemy/goal interactions | [`Systems/PlayerInteractionSystem.cs`](Systems/PlayerInteractionSystem.cs) |
| Idle/run/jump/fall animation state machine | `Program.cs` — `AnimationStateMachine` + `AnimationStateMachineSystem` |
| Camera follow + level bounds | `Program.cs` — `CameraFollow` + `CameraBounds.FromTilemap` |
| Parallax background | `Scenes/background.json` — `ParallaxLayer` on the sky/hills entities |
| Streamed music | `Program.cs` — `StreamingSoundSource` |

## Controls

| Input | Action |
|-------|--------|
| `A` / `D` or `←` / `→` | Move left / right (also pans the debug camera when it's active — see below) |
| `Space` or `↑` | Jump (hold longer for a higher jump; release early to cut it short) |
| `P` | Pause / resume |
| `C` | Toggle the debug free camera (manual pan/zoom/rotate instead of following the player) |
| `W` / `S` (debug camera only) | Pan up / down |
| `Q` / `E` (debug camera only) | Zoom out / in |
| `←` / `→` (debug camera only) | Rotate |
| `R` | Restart after dying or winning; from a game-over, returns to the title screen |
| `ESC` | Exit |
| Mouse | Click title screen / pause menu buttons |
| Gamepad left stick / D-pad | Move left / right |
| Gamepad `A` | Jump |
| Gamepad `Start` | Context-sensitive: play (title) / pause / resume / restart (dead or won) |

## What to notice

- **The player is a `CharacterController2D`**, not an impulse-resolved `BoxCollider2D` — move-and-slide against the tilemap's merged collision, one-way platforms, and the moving platform, with variable jump height from early jump-button release.
- **The level is a code-built `Tilemap`** — two solid tile types (ground, brick), collision generated and merged automatically by `PhysicsWorld2D`'s `TilemapColliderSystem`, no per-tile colliders.
- **Idle/run/jump/fall are an `AnimationStateMachine`** — switching states always restarts at frame 0, and `Sprite.FlipX` (carried alongside the `SpriteSheet` purely for facing) mirrors the character without touching `Transform2D`. The background NPC in `Scenes/background.json`, by contrast, uses a plain `Animation`/`AnimationState` pair with no state machine — set once, looping forever — to show that simpler path side by side with the player's.
- **The title screen, pause menu, and HUD are `UiBuilder`-built UI**, driven by `Systems/GameFlowSystem.cs`'s state machine (`Title` → `Playing` ↔ `Paused` → `Dead`/`Won`). Pausing freezes the whole world (physics, animation, parallax); dying/winning only freezes the player while the level keeps animating behind the message.
- **Coins, the enemies' stomp/damage split, and the goal flag are plain AABB checks** (`Systems/PlayerInteractionSystem.cs`) against the player's controller box — `CharacterController2D` deliberately bypasses `PhysicsWorld2D`'s collider pipeline, so none of this rides on `OnCollisionEnter`. Game code decides what a "hit" means, same as the engine's own docs describe.
- **The enemies and the moving platform are kinematic bodies driven by the same `PlatformPath` helper** — an enemy patrol and a platform patrol are the same mechanism; the enemy's `BoxCollider2D` is just a trigger so it never physically blocks the player.
- **Riding the moving platform** works via `CharacterControllerSystem`'s built-in rider-carrying — no extra code needed in this sample beyond running `PhysicsWorld2D.Update` before `CharacterControllerSystem.Update` each frame.
- **The camera follows with a deadzone and look-ahead**, clamped to the level's bounds via `CameraBounds.FromTilemap`; press `C` to swap that out for a manual pan/zoom/rotate debug camera (`Systems/DebugCameraSystem.cs`), the same `Camera2D` API a standalone camera demo would show.
- **Dust puffs while running and on landing, and a sparkle burst on coin pickup** (`Systems/ParticleEffectsSystem.cs`) all go through the same batched `ParticleSystem` render path as the rest of the sprites.
- **Music streams from OGG Vorbis** (`StreamingSoundSource`); jump/coin/stomp are short one-shots routed through `AudioSystem`'s pooled one-shot voice budget (16 voices) instead of a dedicated `SoundSource` each, so a burst of nearby pickups can never exhaust playback slots — death impacts carry a higher priority so they're never stolen by an ambient coin pickup.
- **The parallax sky/hills layers and a decorative idling NPC are loaded from `Scenes/background.json`** via `SceneLoader`, not hand-built in code — they're the level content that doesn't come from the (code-built, Tiled-less) tilemap.
- **Death is "simple"** per the epic issue's scope: falling into a pit or getting hit by an enemy costs a life and respawns the player at the start instantly — no level reload, no restoring collected coins. Running out of lives returns to the title screen; restarting from the pause menu or the title screen respawns every coin and enemy for a clean run.

## Level layout

- Ground segments with two pits: one plain gap (jump it), one bridged only by the moving platform.
- A one-way platform above the third ground segment — jump up through it from below, land on top, grab the coins waiting there.
- An ascending brick staircase leading to a raised goal plateau; falling off the staircase or the plateau is also a death.
- The goal flag sits at the far end of the plateau — touching it wins.

Character art (`Idle.png`, `Run.png`, `Jump.png`, `Dead.png`) and the particle texture (`particle.png`)
are shared from the now-removed `Samples/Animation2D` and `Samples/ParticleDemo` asset sets.
