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
        { "type": "Transform2D", "position": [0.0, 0.0], "scale": [0.1, 0.1] },
        { "type": "RenderLayer", "value": 5 }
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

Asset paths in `texturePath` (and similar fields) are resolved relative to `AppContext.BaseDirectory` (the directory that contains the application's executable), matching the convention used by `AssetPath.Resolve`, `SceneLoader.Load`, and `SceneSaver.Save`.

## API

### Loading

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
var loader = new SceneLoader(registry);

var scene = loader.Load("Scenes/level1.json");      // throws SceneLoadException on failure
IReadOnlyList<Entity> created = world.Instantiate(scene);

// Tag-based lookup round-trips through the scene file:
var player = world.GetEntity("player");
```

`world.Instantiate(scene)` returns the created entities in the same order as the scene file.

### Saving

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
var saver = new SceneSaver(registry);

saver.Save(world, "Scenes/level1.json");   // writes an indented JSON scene file
```

`SceneSaver` enumerates `world.Entities`, sorts them by ascending `Entity.Id`, and, for each entity, asks every registered `IComponentSerializer` to serialize its component via `TrySerialize(world, entity)`. Serializers that return `null` (e.g. when the entity does not carry that component type) are silently skipped. Paths passed to `Save` are resolved via `AssetPath.Resolve` (against `AppContext.BaseDirectory`), matching the `SceneLoader.Load` convention so a relative path like `"Scenes/level1.json"` targets the same file in both directions.

All six engine-provided serializers support the write direction. Custom serializers opt in by overriding the default `TrySerialize` method on `IComponentSerializer`; they must return a `JsonObject` that includes a non-empty `"type"` field — `SceneSaver` throws `SceneSaveException` if that contract is violated.

### Round-trip

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();

// Save the current world state
new SceneSaver(registry).Save(world, "Scenes/checkpoint.json");

// Later — reload it into a fresh world
var fresh = new World();
fresh.Instantiate(new SceneLoader(registry).Load("Scenes/checkpoint.json"));
```

## Errors

`SceneLoader` is strict — it matches the existing `PrefabLoader` convention. It throws `SceneLoadException` on:

- Malformed JSON
- Missing `entities` array, non-array value, or non-object entries
- Missing `components` inside an entity
- Missing or empty `type` field on a component
- Unknown component `type` — the exception message includes the list of registered types

Tag collisions are detected by `World.CreateEntity(string)` — if a scene uses a tag already bound to an existing entity, `Scene.Apply` lets that exception propagate. This is deliberate: the scene file shouldn't silently drop tags or reuse entities that the user might depend on.

## Not yet supported

- **Cross-entity references** — if entity A should reference entity B, the reference has to be resolved in runtime code after the scene loads. The scene format doesn't support an `entityRef` style yet.
- **Hot reload** — watching the scene file for changes and re-instantiating. Nothing stops you from calling `loader.Load(...)` again manually, but there's no built-in watcher.
- **Scene composition / inheritance** — one scene extending or overriding another. Intentionally deferred.

## See also

- `Samples/SceneDemo/` — end-to-end demo with a seven-entity scene and tag round-trip
- `src/Engine/Yaeger/ECS/SceneSaver.cs` — save-direction implementation
- `src/Engine/Yaeger/ECS/SceneLoader.cs` — implementation
- `src/Engine/Yaeger/ECS/Scene.cs` — in-memory scene representation
- `docs/` — the broader engine docs index
