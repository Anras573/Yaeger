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

## Why hand-written serializers instead of System.Text.Json/Newtonsoft reflection?

`IComponentSerializer` implementations are built *on top of* `System.Text.Json` — `Deserialize`
takes a `JsonElement` and `TrySerialize` returns a `JsonNode`, so all actual JSON
tokenizing/writing goes through STJ. What's hand-written is the mapping layer between JSON and ECS
components, and that layer can't be a generic reflection-based `JsonSerializer.Deserialize<T>()`
call for a few structural reasons:

- **A component doesn't just deserialize into a value — it has to add itself to a `World`.**
  `Deserialize` returns `Action<World, Entity>`, not a component instance. That's what lets
  `Parent` resolve its `parentTag` against `World.GetEntity` at *apply* time, after every entity in
  the file already exists (see [Cross-entity references](#cross-entity-references) below) — a
  generic deserializer has no concept of deferring construction until the rest of the entity graph
  is in place.
- **The `"type"` string dispatches to one of many `struct` component types via a runtime-extensible
  registry** (`ComponentRegistry`), not a closed hierarchy attributes can describe. Components have
  no shared base class (`ComponentStorage<T>` requires `where T : struct`), and game code registers
  its own serializers alongside the engine's at runtime.
- **The assembly split forces it.** `Yaeger.Core` has no Silk.NET dependency, but a component like
  `Text` only compiles into the native `Yaeger` assembly. Its serializer lives under
  `Serializers/Native/` and is only registered by `RegisterNativeEngineComponents()` — two
  `IComponentSerializer` implementations model that split trivially; one reflection-driven
  converter couldn't.
- **Round-tripping is deliberately asymmetric per component.** `MeshHandle` is intentionally left
  unserializable (its `Id` is a runtime-assigned registry key, not portable across runs) — see
  [Saving](#saving) below. Lenient input shapes (`Transform2DSerializer` accepts a `Vector2` as
  either `[x, y]` or `{ "x": ..., "y": ... }`) and field-specific errors (`ComponentRegistry`
  listing all registered types when an unknown one is used) are also easier to reason about as
  plain code than as converter attributes.

In short: reflection-based (de)serialization is a great fit when JSON mirrors a POCO's shape, but
here JSON has to drive a struct-based ECS with deferred cross-entity references, a
runtime-extensible type registry, and an assembly boundary that changes which components even
exist per runtime. A hand-written `IComponentSerializer` per component ends up no larger than the
custom `JsonConverter<T>` that would otherwise be needed per component — and additionally gets the
deferred-apply hook and the opt-out write direction for free.

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

### Lifecycle: unload and swap

`world.Instantiate(scene)` hands back a plain entity list and the engine's involvement ends there —
fine for a scene that lives for the whole program, but a lift interior giving way to a hangar, a
level transition, or a menu returning to gameplay all need to tear a previously loaded set of
entities back down again. `world.LoadScene(scene)` does the same spawning as `Instantiate`, but
returns a `SceneInstance` handle that remembers exactly which entities it created:

```csharp
var instance = world.LoadScene(scene);

// ...later, when this scene is no longer needed:
instance.Unload(); // destroys exactly instance.Entities — nothing else
```

`Unload()` is idempotent (a second call is a silent no-op) and destroys its entities via plain
`world.DestroyEntity` — not `world.DestroyHierarchy` (see [hierarchy.md](hierarchy.md)) — because a
scene's own `Parent`-linked children are already part of its `Entities` list (`Scene.Apply` creates
every entity the file describes, parents and children alike). Walking the hierarchy instead would
risk destroying another instance's entities too, if something outside this scene ever got parented
onto one of its entities after load.

Loading is **additive**: call `LoadScene` as many times as needed, and unload each returned
instance independently — nothing assumes only one scene is loaded at a time, and unloading one
instance never touches another's entities, even if both came from the same `Scene`.

`world.SwapScene(previous, next)` is the common transition case — unload `previous` (or do nothing
if it's `null`, e.g. a first load) and load `next`:

```csharp
var hangar = world.SwapScene(liftInterior, hangarScene);
```

Unloading before loading matters when the two scenes share tags: `DestroyEntity` frees a tag's
binding immediately, so `next` can reuse any tag `previous` held without a rebind race.

`Instantiate(Scene)` is unchanged and still the right call for entities that should just accumulate
in the world for the program's lifetime — `LoadScene`/`SceneInstance` only add bookkeeping for the
cases that need to unload later.

### Saving

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
var saver = new SceneSaver(registry);

saver.Save(world, "Scenes/level1.json");   // writes an indented JSON scene file
```

`SceneSaver` enumerates `world.Entities`, sorts them by ascending `Entity.Id`, and, for each entity, asks every registered `IComponentSerializer` to serialize its component via `TrySerialize(world, entity)`. Serializers that return `null` (e.g. when the entity does not carry that component type) are silently skipped. Paths passed to `Save` are resolved via `AssetPath.Resolve` (against `AppContext.BaseDirectory`), matching the `SceneLoader.Load` convention so a relative path like `"Scenes/level1.json"` targets the same file in both directions.

All engine-provided serializers support the write direction. `RegisterEngineComponents()` registers serializers for the 2D components (`Sprite`, `Transform2D`, `SpriteSheet`, `Animation`, `AnimationState`, `AnimationStateMachine`, `RenderLayer`, `Tilemap`, `Camera2D`, `ParticleEmitter`, `ParallaxLayer`, `BoxCollider2D`, `CircleCollider2D`, `RigidBody2D`, `Velocity2D`, `PhysicsMaterial`, `LocalTransform2D`), the 3D components (`Transform3D`, `Camera3D`, `Material3D`, `DirectionalLight`, `PointLight`, `SpotLight`, `LocalTransform3D`), and the hierarchy component `Parent` (see [hierarchy.md](hierarchy.md) — saving one requires the parent entity to be tagged, since a tag is the only way to express the reference in JSON). `MeshHandle` is deliberately excluded — its `Id` is a runtime-assigned `GpuMeshRegistry` key that isn't portable across runs, so meshes are re-assigned in code rather than persisted. The screen-space UI components (`UiRect`, `UiPanel`, `UiButton`, `UiLabel`) are not yet registered — whether screen-space UI belongs in world scenes is still an open design question. Custom serializers opt in by overriding the default `TrySerialize` method on `IComponentSerializer`; they must return a `JsonObject` that includes a non-empty `"type"` field — `SceneSaver` throws `SceneSaveException` if that contract is violated.

`Text` is a special case: it holds a native `Yaeger.Font.Font` reference with no `Yaeger.Core` equivalent, so its serializer can only compile into the native runtime and isn't part of the shared `RegisterEngineComponents()`. Native games that want `Text` to round-trip through scenes should call `RegisterNativeEngineComponents()` instead — it registers everything `RegisterEngineComponents()` does, plus `Text`. Like `Sprite.TexturePath`, `Text` round-trips its `FontHandle` by *path*, not the native `Font` instance it may have been constructed with; the path is resolved into a font the same way at load time.

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

## Cross-entity references

`Scene.Apply` creates every entity in the file (registering its tag) before applying any
component, so a component can resolve another entity's tag at apply time regardless of where that
entity appears in the file — including later than itself. `Parent` (see [hierarchy.md](hierarchy.md))
is the built-in example: its `parentTag` field is resolved via `World.GetEntity` when the
component is applied, so a child can be listed before its parent in the scene file. Custom
serializers can use the same technique for other entity-to-entity references — there's no generic
`entityRef` JSON shape, but tags plus deferred resolution inside the `Action<World, Entity>`
returned from `Deserialize` cover it.

## Not yet supported

- **Scene composition / inheritance** — one scene extending or overriding another. Intentionally deferred.
- **Async / streaming / background-thread loading** — `LoadScene`/`Instantiate` are synchronous.
- **Fade or dissolve transitions between scenes** — a post-processing concern, layered on top of
  `PostProcessStack` rather than the scene lifecycle itself.
- **Persisting runtime state across an unload/reload cycle** — `Unload()` simply destroys entities;
  nothing is snapshotted. Save state yourself first (e.g. via `SceneSaver`) if a reload needs it.

## See also

- `Samples/Platformer/Scenes/background.json` — end-to-end demo: parallax layers and a decorative
  animated NPC (entities the level's tilemap doesn't carry) loaded via `SceneLoader`, see
  `Samples/Platformer/Program.cs`
- `src/Engine/Yaeger/ECS/SceneSaver.cs` — save-direction implementation
- `src/Engine/Yaeger/ECS/SceneLoader.cs` — implementation
- `src/Engine/Yaeger/ECS/Scene.cs` — in-memory scene representation
- `src/Engine/Yaeger/ECS/SceneInstance.cs` — the unload/swap lifecycle handle
- [hierarchy.md](hierarchy.md) — `world.DestroyHierarchy`, the cascading counterpart to `DestroyEntity`
- [`asset-hot-reload.md`](asset-hot-reload.md) — watching a scene file for changes and re-instantiating via `SceneHotReload`
- `docs/` — the broader engine docs index
