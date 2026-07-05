# Tilemaps

A tilemap describes a level as a rectangular grid of tile indices referencing a shared
tileset texture, instead of one entity per tile. The engine provides:

- **`Tileset`** — a texture divided into a uniform grid of equally-sized tiles, with
  per-tile UV lookup (`GetTileUv`). Uses the same UV math as `SpriteSheet`.
- **`Tilemap`** — an ECS component holding the grid (`Width` × `Height` tile indices),
  the tile size in world units, and a tint. Pair it with a `Transform2D`.
- Rendering via **`UnifiedRenderSystem`** — tilemaps are drawn in the same
  `RenderLayer`-sorted pass as sprites and text, and all tiles of a map share the
  tileset texture, so each map collapses into a single draw call in the batched
  renderer.

## Coordinate conventions

- The entity's `Transform2D.Position` is the **bottom-left corner** of the map in world
  space.
- Tile indices are stored row-major with **row 0 at the top** of the map — the same
  order the grid reads when written as text or exported from editors such as Tiled.
- Cell `(column, row)` therefore occupies the world-space cell whose bottom-left corner
  is `(column * TileSize.X, (Height - 1 - row) * TileSize.Y)` relative to the entity's
  position.
- `Tilemap.EmptyTile` (`-1`) marks an empty cell; nothing is rendered for it.

## Usage

```csharp
var tileset = new Tileset("Assets/tiles.png", columns: 8, rows: 4);

// 6 wide, 3 high; row 0 is the top row.
var tilemap = new Tilemap(
    tileset,
    width: 6,
    height: 3,
    tiles:
    [
        -1, -1, -1, -1, -1, -1,
        -1, -1,  4,  5, -1, -1,
         0,  1,  1,  1,  1,  2,
    ]
);

var level = world.CreateEntity("level");
world.AddComponent(level, new Transform2D(new Vector2(0f, 0f)));
world.AddComponent(level, tilemap);
world.AddComponent(level, new RenderLayer(0)); // background layer
```

Edit cells at runtime with `SetTile` / `GetTile`:

```csharp
tilemap.SetTile(column: 2, row: 1, tileIndex: 7);
tilemap.SetTile(column: 2, row: 1, Tilemap.EmptyTile); // clear the cell
```

The tile array is shared between copies of the component (like `Animation.Frames`), so
mutating a copy retrieved from the `World` updates the rendered map without re-adding
the component.

### Layering

Use multiple tilemap entities with different `RenderLayer` values for
background/foreground layers. Layer ordering is shared with sprites and text, so a
player sprite on layer 1 renders between a background map on layer 0 and a foreground
map on layer 2.

### Culling

When a `Camera2D` is active (a `UnifiedRenderSystem` constructed with a `Window` and a
camera entity present), only the tiles intersecting the camera's visible span are
submitted each frame. Maps with a rotated `Transform2D` fall back to submitting every
tile (conservative — never culls a visible tile).

## Scenes and prefabs

`Tilemap` is registered with `ComponentRegistry.RegisterEngineComponents()` under the
type id `"Tilemap"`, so tilemaps load from prefab/scene JSON and round-trip through
`SceneSaver`:

```json
{
  "type": "Tilemap",
  "texturePath": "Assets/tiles.png",
  "columns": 8,
  "rows": 4,
  "width": 6,
  "height": 3,
  "tileWidth": 1.0,
  "tileHeight": 1.0,
  "tiles": [-1, -1, -1, -1, -1, -1, -1, -1, 4, 5, -1, -1, 0, 1, 1, 1, 1, 2],
  "tint": [255, 255, 255, 255]
}
```

`rows` defaults to `1`, `tileWidth`/`tileHeight` default to `1.0`, `tiles` defaults to
an all-empty map, and `tint` defaults to white when absent.
