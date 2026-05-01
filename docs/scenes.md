# Scenes

A **scene** is a JSON file that declares a collection of entities and the components each one carries. The `SceneLoader` reads a scene file and produces a `Scene` object; `world.Instantiate(scene)` spawns every entity into the world, restoring tags for the entities that declared them.

Scenes extend the existing prefab pipeline — they reuse `ComponentRegistry` and `IComponentSerializer`, so any component that can appear in a `.prefab.json` can also appear inside a scene.

## File format

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
        { "type": "Sprite", "texturePath": "Assets/pickup.png" },
        { "type": "Transform2D", "position": [0.3, 0.3], "scale": [0.05, 0.05] }
      ]
    }
  ]
}
```

- `entities` (required) — an array of entity objects, preserved in order.
- `tag` (optional, per entity) — a string; the runtime tag the entity is created with. If omitted, the entity is anonymous.
- `components` (required, per entity) — an array of component objects. Each component has a `type` field matching an `IComponentSerializer.TypeId` registered on the `ComponentRegistry`.

Asset paths in `texturePath` (and similar fields) are resolved relative to the executable's working directory, matching the existing `Prefab` and `Texture` conventions.

## API

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
var loader = new SceneLoader(registry);

var scene = loader.Load("Scenes/level1.json");      // throws SceneLoadException on failure
IReadOnlyList<Entity> created = world.Instantiate(scene);

// Tag-based lookup round-trips through the scene file:
var player = world.GetEntity("player");
```

`world.Instantiate(scene)` returns the created entities in the same order as the scene file.

## Errors

`SceneLoader` is strict — it matches the existing `PrefabLoader` convention. It throws `SceneLoadException` on:

- Malformed JSON
- Missing `entities` array, non-array value, or non-object entries
- Missing `components` inside an entity
- Missing or empty `type` field on a component
- Unknown component `type` — the exception message includes the list of registered types

Tag collisions are detected by `World.CreateEntity(string)` — if a scene uses a tag already bound to an existing entity, `Scene.Apply` lets that exception propagate. This is deliberate: the scene file shouldn't silently drop tags or reuse entities that the user might depend on.

## Not yet supported

- **Saving scenes** — `Scene` is load-only today. Writing a world back out as JSON requires adding a symmetric write method to `IComponentSerializer` and a `SceneSaver`; this is planned for the inspector work (issue #36) where it has a caller.
- **Cross-entity references** — if entity A should reference entity B, the reference has to be resolved in runtime code after the scene loads. The scene format doesn't support an `entityRef` style yet.
- **Hot reload** — watching the scene file for changes and re-instantiating. Nothing stops you from calling `loader.Load(...)` again manually, but there's no built-in watcher.
- **Scene composition / inheritance** — one scene extending or overriding another. Intentionally deferred.

## See also

- `Samples/SceneDemo/` — end-to-end demo with a seven-entity scene and tag round-trip
- `src/Engine/Yaeger/ECS/SceneLoader.cs` — implementation
- `src/Engine/Yaeger/ECS/Scene.cs` — in-memory scene representation
- `docs/` — the broader engine docs index
