# Platformer

The integration proof for the "platformer support" epic: one complete, playable level exercising
every feature that epic added, composed the way a real game would in `Program.cs`.

## How to Run

```bash
dotnet run --project Samples/Platformer/Platformer.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Controls

| Input | Action |
|-------|--------|
| `A` / `D` or `←` / `→` | Move left / right |
| `Space` or `↑` | Jump (hold longer for a higher jump; release early to cut it short) |
| `R` | Restart after dying or winning |
| `ESC` | Exit |
| Gamepad left stick / D-pad | Move left / right |
| Gamepad `A` | Jump |
| Gamepad `Start` | Restart |

## What to notice

- **The player is a `CharacterController2D`**, not an impulse-resolved `BoxCollider2D` — move-and-slide against the tilemap's merged collision, one-way platforms, and the moving platform, with variable jump height from early jump-button release.
- **The level is a code-built `Tilemap`** — two solid tile types (ground, brick), collision generated and merged automatically by `PhysicsWorld2D`'s `TilemapColliderSystem`, no per-tile colliders.
- **Idle/run/jump/fall are an `AnimationStateMachine`** — switching states always restarts at frame 0, and `Sprite.FlipX` (carried alongside the `SpriteSheet` purely for facing) mirrors the character without touching `Transform2D`.
- **Coins, the enemies' stomp/damage split, and the goal flag are plain AABB checks** (`Systems/PlayerInteractionSystem.cs`) against the player's controller box — `CharacterController2D` deliberately bypasses `PhysicsWorld2D`'s collider pipeline, so none of this rides on `OnCollisionEnter`. Game code decides what a "hit" means, same as the engine's own docs describe.
- **The enemies and the moving platform are kinematic bodies driven by the same `PlatformPath` helper** — an enemy patrol and a platform patrol are the same mechanism; the enemy's `BoxCollider2D` is just a trigger so it never physically blocks the player.
- **Riding the moving platform** works via `CharacterControllerSystem`'s built-in rider-carrying — no extra code needed in this sample beyond running `PhysicsWorld2D.Update` before `CharacterControllerSystem.Update` each frame.
- **The camera follows with a deadzone and look-ahead**, clamped to the level's bounds via `CameraBounds.FromTilemap`; two parallax layers (sky, hills) scroll behind it at different speeds.
- **Music streams from OGG Vorbis** (`StreamingSoundSource`); jump/coin/stomp are short fully-decoded WAV one-shots (`SoundBuffer`/`SoundSource`).
- **Death is "simple"** per the epic issue's scope: falling into a pit or getting hit by an enemy respawns the player at the start instantly — no separate lives system, no level reload, no restoring collected coins.

## Level layout

- Ground segments with two pits: one plain gap (jump it), one bridged only by the moving platform.
- A one-way platform above the third ground segment — jump up through it from below, land on top, grab the coins waiting there.
- An ascending brick staircase leading to a raised goal plateau; falling off the staircase or the plateau is also a death.
- The goal flag sits at the far end of the plateau — touching it wins.

Character art (`Idle.png`, `Run.png`, `Jump.png`, `Dead.png`) is shared from `Samples/Animation2D`'s asset set.
