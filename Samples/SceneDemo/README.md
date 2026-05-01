# Scene Demo

Demonstrates the JSON scene loader: a multi-entity world is described in `Scenes/level1.json` and spawned with a single `world.Instantiate(scene)` call.

## How to Run

```bash
dotnet run --project Samples/SceneDemo/SceneDemo.csproj
```

**Note:** Requires a display. `System.PlatformNotSupportedException` in headless environments is expected.

## Controls

| Key | Action |
|-----|--------|
| ESC | Exit |

## What to notice

- **One file, many entities.** `Scenes/level1.json` declares seven entities — a `ground` bar, a `player` square, a `sun` square, and four anonymous "star" squares. The sample spawns all of them with one loader call.
- **Tag round-trip.** Three of the entities have tags (`ground`, `player`, `sun`). After `world.Instantiate(scene)`, we look `player` up with `world.GetEntity("player")` and animate it — proving that the tag the JSON file declared is the tag the runtime world sees.
- **Anonymous entities are first-class.** The four stars don't carry tags; they just appear at the positions the scene file declared. Mixing tagged and anonymous entities in the same file is the intended usage pattern.
- **Scenes compose with runtime systems.** The player's animation (the orbit motion) is pure runtime C# reading and writing the entity that the scene file created. Scenes describe initial state, not behaviour — behaviour stays in code.

## The scene format

```json
{
  "entities": [
    {
      "tag": "player",
      "components": [
        { "type": "Sprite", "texturePath": "Assets/player.png" },
        { "type": "Transform2D", "position": [0.0, 0.0], "scale": [0.1, 0.1] }
      ]
    },
    {
      "components": [
        { "type": "Sprite", "texturePath": "Assets/pickup.png" }
      ]
    }
  ]
}
```

- `tag` is optional. Omit it for anonymous entities.
- `components` is required. Each component is a JSON object with a `type` field matching an `IComponentSerializer.TypeId`.
- Unknown component types fail loudly with the list of registered types.

See `docs/scenes.md` for the full schema and API reference.
