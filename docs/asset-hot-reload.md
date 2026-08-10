# Asset Hot-Reload

`AssetWatcher` is an opt-in, dev-time-only watcher that notices when a texture or scene file
changes on disk and lets the running game reload it without a restart. It is off by default —
nothing in the engine wires it up automatically — and it is native-only (`Yaeger`, built on
`System.IO.FileSystemWatcher`); there is no `Yaeger.Browser` equivalent.

Save a sprite in your paint tool, alt-tab back to the running game, and the texture updates within
a frame or two. It pairs well with the [editor overlay](editor.md) for a tight art/level tweak
loop.

## Wiring it up

`AssetWatcher.ForDirectory()` watches a directory recursively (default: the app's base directory,
matching `AssetPath.Resolve`/`NativeAssetResolver`) and raises `AssetChanged` with each changed
file's path relative to that directory — the same form of path used by `Sprite.TexturePath`,
`Material3D` texture paths, and scene JSON. Call `Update` once per frame so reload handlers run on
the main thread, where GL calls are safe:

```csharp
using var window = Window.Create();
var textures = new TextureManager(window.Gl);

// ... build your renderer, world, sprites/materials that reference textures by path ...

using var assetWatcher = AssetWatcher.ForDirectory();

// Re-upload changed textures into the existing GL texture object — every Sprite/Material3D
// referencing the path picks up the change with no handle churn. Reload() is a safe no-op for
// paths that were never loaded as a texture in the first place.
assetWatcher.AssetChanged += path => textures.Reload(path);

window.OnUpdate += deltaTime => assetWatcher.Update((float)deltaTime);

window.Run();
```

`AssetWatcher` debounces raw filesystem notifications (`debounceSeconds`, default `0.25f`) —
editors commonly write a file more than once per save (temp file + rename, or several flushes), so
a path only dispatches once it has gone quiet for that long. A malformed or half-written file
(caught mid-save) is survived: `TextureManager.Reload`/`Texture.TryReload` decode the file fully
before touching any GL state, so a bad read leaves the existing texture on screen untouched and
logs a warning to `Console.Error` instead of crashing.

## Reloading textures

`TextureManager.Reload(path)` looks up the `Texture` already cached for `path` and re-uploads the
file's current contents into that texture's *existing* GL handle — object identity is preserved,
so anything holding a reference to the `Texture` (or resolving it by path through
`TextureManager.Get` each frame, as `Renderer`/`Renderer3D` do) sees the update with no extra
wiring. It returns `false` and does nothing if `path` was never loaded via `Get`, which is why the
snippet above can call it unconditionally for every changed file — it only does work for paths that
are actually textures.

## Reloading scenes

Scenes are different: re-instantiating one means deciding what happens to the entities the
*previous* load created, and that's world state the engine doesn't own. `SceneHotReload` reflects
that — it re-parses the scene file and hands you the fresh `Scene` via a `Reloaded` event rather
than touching the `World` itself:

```csharp
var registry = new ComponentRegistry().RegisterEngineComponents();
var loader = new SceneLoader(registry);
var sceneHotReload = new SceneHotReload(loader, "Scenes/level1.json");

var liveEntities = world.Instantiate(loader.Load("Scenes/level1.json"));

sceneHotReload.Reloaded += scene =>
{
    foreach (var entity in liveEntities)
        world.DestroyEntity(entity);

    liveEntities = world.Instantiate(scene);
};

assetWatcher.AssetChanged += path =>
{
    if (path == sceneHotReload.Path)
        sceneHotReload.Reload();
};
```

A malformed or mid-write scene file is survived the same way as textures: `Reload()` catches
`SceneLoadException`/`IOException`, logs a warning, and simply doesn't raise `Reloaded` — the world
keeps whatever it already had.

## Configuration

| Option | Effect |
| --- | --- |
| `AssetWatcher.ForDirectory(root, debounceSeconds)` | `root` defaults to `AppContext.BaseDirectory`; pass a different directory to watch a different asset root |
| `debounceSeconds` | How long a path must go without a new raw notification before it dispatches (default `0.25f`) |
| `new AssetWatcher(IFileChangeSource, root, debounceSeconds)` | Lower-level constructor — used internally by `ForDirectory`, and by tests to inject a fake `IFileChangeSource` instead of a real `FileSystemWatcher` |

## Out of scope

- **Shaders** — GLSL is compiled from `EmbeddedResource`s, not loaded from disk at runtime, so
  there's nothing to watch.
- **Meshes/models and fonts** — not covered yet. Mesh reload in particular needs more care, since
  `GpuMeshRegistry` handle identity (`MeshHandle`) would need to survive a reload the same way
  `Texture` identity does here.
- **Production builds** — `AssetWatcher` is a development convenience you opt into explicitly;
  nothing in the engine starts a `FileSystemWatcher` on its own.

## See also

- `src/Engine/Yaeger/Assets/HotReload/AssetWatcher.cs` — debounce/dispatch implementation
- `src/Engine/Yaeger/Assets/HotReload/IFileChangeSource.cs` / `FileSystemWatcherSource.cs` — the mockable watcher seam
- `src/Engine/Yaeger/Assets/HotReload/SceneHotReload.cs` — scene reload callback API
- `src/Engine/Yaeger/Rendering/Texture.cs` (`TryReload`) / `TextureManager.cs` (`Reload`) — texture re-upload
- [`scenes.md`](scenes.md) — the scene file format and `SceneLoader`/`SceneSaver` API
- [`editor.md`](editor.md) — the in-game inspector this pairs with for a full tweak loop
