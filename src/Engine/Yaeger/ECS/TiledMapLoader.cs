using System.Numerics;
using System.Text.Json;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.ECS;

/// <summary>
/// Loads a <see cref="Scene"/> from a level authored in
/// <a href="https://www.mapeditor.org/">Tiled</a>'s JSON map format (<c>.tmj</c>).
/// </summary>
/// <remarks>
/// <para>
/// Tile layers become one entity each, carrying a <see cref="Transform2D"/>, a
/// <see cref="Tilemap"/> built from the map's tileset, and a <see cref="RenderLayer"/> whose
/// value is the tile layer's position in the map's <c>layers</c> array (so later — visually
/// higher — Tiled layers draw on top, matching Tiled's own bottom-to-top layer list order).
/// </para>
/// <para>
/// Object layers spawn one entity per object whose Tiled <c>class</c> (Tiled 1.9+) or
/// <c>type</c> (older Tiled versions; used as a fallback when <c>class</c> is absent) matches a
/// key in the <c>prefabsByName</c> dictionary passed to <see cref="Load"/>/<see cref="Parse"/>.
/// The matched <see cref="Prefab"/> is applied and then given a <see cref="Transform2D"/> at the
/// object's position (so a <c>Transform2D</c> baked into the prefab itself is overridden).
/// Objects with no matching prefab, or with <c>"visible": false</c>, are skipped without error —
/// not every marker an author drops in Tiled needs a corresponding prefab.
/// </para>
/// <para>
/// Solid-tile flags for <see cref="Physics.Systems.TilemapColliderSystem"/> are read from each
/// tileset tile's custom boolean property named <c>solid</c> (case-insensitive), set in Tiled's
/// tileset editor.
/// </para>
/// <para><b>Scope</b> — this is an import-only loader for a common subset of the format:</para>
/// <list type="bullet">
///   <item>Only <c>"orientation": "orthogonal"</c>, finite maps are supported (no isometric,
///     hexagonal, or infinite maps).</item>
///   <item>Only a single, <b>embedded</b> tileset per map is supported — external tileset
///     references (a <c>"source"</c> field instead of inline tileset data) are rejected. Tiled
///     embeds a new tileset by default; this only becomes a problem if you explicitly extract
///     it to its own <c>.tsj</c> file.</item>
///   <item>Tile layer data must use the default (uncompressed) array/CSV encoding — Base64 and
///     compressed (<c>zlib</c>/<c>gzip</c>/<c>zstd</c>) layer data are rejected.</item>
///   <item>Flipped/rotated tile GIDs render as their unflipped base tile (the flip flags are
///     stripped, not applied).</item>
///   <item>Image layers and group layers are ignored.</item>
///   <item>There is no export/round-trip path — only import.</item>
/// </list>
/// </remarks>
public sealed class TiledMapLoader
{
    private readonly IAssetResolver _assetResolver;

    /// <summary>
    /// Initializes a new <see cref="TiledMapLoader"/>.
    /// </summary>
    /// <param name="assetResolver">
    /// Resolver used to locate the map file passed to <see cref="Load"/>. Defaults to
    /// <see cref="DefaultAssetResolver"/> (resolves against <see cref="AppContext.BaseDirectory"/>).
    /// </param>
    public TiledMapLoader(IAssetResolver? assetResolver = null)
    {
        _assetResolver = assetResolver ?? new DefaultAssetResolver();
    }

    /// <summary>
    /// Loads a Tiled <c>.tmj</c> map file from disk and converts it into a <see cref="Scene"/>.
    /// </summary>
    /// <param name="path">Path to the <c>.tmj</c> file, resolved via the configured resolver.</param>
    /// <param name="prefabsByName">
    /// Maps a Tiled object's <c>class</c>/<c>type</c> name to the <see cref="Prefab"/> spawned
    /// for matching objects. Objects with no entry here are skipped. May be <c>null</c> to skip
    /// every object layer.
    /// </param>
    /// <param name="tileSize">World-unit size of one tile. Defaults to <c>(1, 1)</c>.</param>
    /// <param name="origin">
    /// World position of the map's bottom-left corner (matching <see cref="Tilemap"/>'s own
    /// anchor convention). Defaults to <see cref="Vector2.Zero"/>.
    /// </param>
    /// <exception cref="FileNotFoundException">When the file does not exist.</exception>
    /// <exception cref="TiledMapLoadException">
    /// When the JSON is malformed or uses an unsupported feature (see the type-level remarks).
    /// </exception>
    public Scene Load(
        string path,
        IReadOnlyDictionary<string, Prefab>? prefabsByName = null,
        Vector2? tileSize = null,
        Vector2? origin = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        var resolved = _assetResolver.Resolve(path);
        if (!File.Exists(resolved))
            throw new FileNotFoundException($"Tiled map file not found: {path}", resolved);

        var json = File.ReadAllText(resolved);
        var mapDirectory = Path.GetDirectoryName(resolved);
        return Parse(json, prefabsByName, tileSize, origin, mapDirectory);
    }

    /// <summary>
    /// Parses a Tiled <c>.tmj</c> map from a JSON string into a <see cref="Scene"/>.
    /// </summary>
    /// <param name="json">The map JSON text.</param>
    /// <param name="prefabsByName">See <see cref="Load"/>.</param>
    /// <param name="tileSize">See <see cref="Load"/>.</param>
    /// <param name="origin">See <see cref="Load"/>.</param>
    /// <param name="mapDirectory">
    /// Directory the map file lives in, used to resolve the tileset's <c>image</c> path when it
    /// is relative. Passed automatically by <see cref="Load"/>; pass explicitly when calling
    /// <see cref="Parse"/> directly with a relative tileset image path. When <c>null</c>, a
    /// relative image path is stored on the <see cref="Tileset"/> as-is.
    /// </param>
    /// <exception cref="TiledMapLoadException">
    /// When the JSON is malformed or uses an unsupported feature (see the type-level remarks).
    /// </exception>
    public Scene Parse(
        string json,
        IReadOnlyDictionary<string, Prefab>? prefabsByName = null,
        Vector2? tileSize = null,
        Vector2? origin = null,
        string? mapDirectory = null
    )
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new TiledMapLoadException("Tiled map JSON must be a non-empty string.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new TiledMapLoadException("Failed to parse Tiled map JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new TiledMapLoadException("Tiled map JSON root must be a JSON object.");

            var orientation = GetOptionalString(root, "orientation") ?? "orthogonal";
            if (orientation != "orthogonal")
                throw new TiledMapLoadException(
                    $"Unsupported map orientation '{orientation}' — only 'orthogonal' maps are supported."
                );

            if (GetOptionalBool(root, "infinite") ?? false)
                throw new TiledMapLoadException(
                    "Infinite maps are not supported — disable 'Infinite map' in Tiled's map properties."
                );

            var tileWidthPx = GetRequiredPositiveInt(root, "tilewidth", "map");
            var tileHeightPx = GetRequiredPositiveInt(root, "tileheight", "map");
            var mapHeightTiles = GetRequiredPositiveInt(root, "height", "map");
            GetRequiredPositiveInt(root, "width", "map"); // validated, unused beyond validation

            var tileset = ParseTileset(root, mapDirectory, out var firstGid);

            var tileSizeWorldUnits = tileSize ?? Vector2.One;
            var originVec = origin ?? Vector2.Zero;
            var mapHeightPx = mapHeightTiles * tileHeightPx;

            var layers = GetRequiredArray(root, "layers", "map");

            var entries = new List<Scene.SceneEntityEntry>();
            var renderLayerIndex = 0;

            foreach (var layerEl in layers.EnumerateArray())
            {
                var layerType = GetRequiredString(layerEl, "type", "layer");
                switch (layerType)
                {
                    case "tilelayer":
                        entries.Add(
                            ParseTileLayer(
                                layerEl,
                                tileset,
                                firstGid,
                                tileWidthPx,
                                tileHeightPx,
                                tileSizeWorldUnits,
                                originVec,
                                renderLayerIndex
                            )
                        );
                        renderLayerIndex++;
                        break;

                    case "objectgroup":
                        entries.AddRange(
                            ParseObjectLayer(
                                layerEl,
                                prefabsByName,
                                tileWidthPx,
                                tileHeightPx,
                                mapHeightPx,
                                tileSizeWorldUnits,
                                originVec
                            )
                        );
                        break;

                    default:
                        // Image layers and groups are out of scope for this import pass.
                        break;
                }
            }

            return new Scene(entries);
        }
    }

    private static Tileset ParseTileset(JsonElement root, string? mapDirectory, out int firstGid)
    {
        var tilesetsEl = GetRequiredArray(root, "tilesets", "map");

        var tilesetList = tilesetsEl.EnumerateArray().ToList();
        if (tilesetList.Count != 1)
            throw new TiledMapLoadException(
                $"Only a single tileset per map is supported (found {tilesetList.Count}). "
                    + "Merge your tiles into one tileset in Tiled."
            );

        var tilesetEl = tilesetList[0];

        if (tilesetEl.TryGetProperty("source", out _))
            throw new TiledMapLoadException(
                "External tileset references ('source') are not supported — embed the tileset "
                    + "in the map file (Tiled's default; do not extract it to a separate .tsj)."
            );

        firstGid = GetRequiredPositiveInt(tilesetEl, "firstgid", "tileset");
        var imagePath = GetRequiredString(tilesetEl, "image", "tileset");
        var columns = GetRequiredPositiveInt(tilesetEl, "columns", "tileset");
        var tileCount = GetRequiredPositiveInt(tilesetEl, "tilecount", "tileset");

        if (tileCount % columns != 0)
            throw new TiledMapLoadException(
                $"Tileset tile count ({tileCount}) is not evenly divisible by its column count ({columns})."
            );
        var rows = tileCount / columns;

        var solidTileIndices = ParseSolidTileIndices(tilesetEl);
        var resolvedImagePath = ResolveImagePath(imagePath, mapDirectory);

        try
        {
            return new Tileset(resolvedImagePath, columns, rows, solidTileIndices);
        }
        catch (ArgumentException ex)
        {
            throw new TiledMapLoadException(
                $"Tileset '{imagePath}' produced an invalid Tileset: {ex.Message}",
                ex
            );
        }
    }

    private static List<int>? ParseSolidTileIndices(JsonElement tilesetEl)
    {
        if (!tilesetEl.TryGetProperty("tiles", out var tilesEl))
            return null;

        if (tilesEl.ValueKind != JsonValueKind.Array)
            throw new TiledMapLoadException("Tileset 'tiles' must be an array when present.");

        List<int>? solid = null;
        foreach (var tileEl in tilesEl.EnumerateArray())
        {
            if (!tileEl.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var localId))
                throw new TiledMapLoadException(
                    "Each tileset 'tiles' entry must have an integer 'id'."
                );

            if (
                !tileEl.TryGetProperty("properties", out var propertiesEl)
                || propertiesEl.ValueKind != JsonValueKind.Array
            )
                continue;

            foreach (var propertyEl in propertiesEl.EnumerateArray())
            {
                if (
                    !propertyEl.TryGetProperty("name", out var nameEl)
                    || nameEl.ValueKind != JsonValueKind.String
                    || !string.Equals(
                        nameEl.GetString(),
                        "solid",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    continue;

                if (
                    propertyEl.TryGetProperty("value", out var valueEl)
                    && valueEl.ValueKind == JsonValueKind.True
                )
                {
                    solid ??= [];
                    solid.Add(localId);
                }
            }
        }

        return solid;
    }

    private static string ResolveImagePath(string rawImagePath, string? mapDirectory)
    {
        if (Path.IsPathRooted(rawImagePath) || mapDirectory is null)
            return rawImagePath;

        return Path.GetFullPath(Path.Combine(mapDirectory, rawImagePath));
    }

    private static Scene.SceneEntityEntry ParseTileLayer(
        JsonElement layerEl,
        Tileset tileset,
        int firstGid,
        int tileWidthPx,
        int tileHeightPx,
        Vector2 tileSizeWorldUnits,
        Vector2 originVec,
        int renderLayerIndex
    )
    {
        var name = GetOptionalString(layerEl, "name");
        var context = name is null ? "tile layer" : $"tile layer '{name}'";
        var layerWidth = GetRequiredPositiveInt(layerEl, "width", context);
        var layerHeight = GetRequiredPositiveInt(layerEl, "height", context);

        if (
            GetOptionalString(layerEl, "encoding") is { } encoding
            && !string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase)
        )
            throw new TiledMapLoadException(
                $"{context} uses unsupported encoding '{encoding}' — only the default "
                    + "(uncompressed) array/CSV encoding is supported."
            );

        if (GetOptionalString(layerEl, "compression") is { } compression)
            throw new TiledMapLoadException(
                $"{context} uses compression '{compression}', which is not supported."
            );

        var dataEl = GetRequiredArray(layerEl, "data", context);
        var cellCount = layerWidth * layerHeight;
        var tiles = new int[cellCount];
        var index = 0;

        foreach (var gidEl in dataEl.EnumerateArray())
        {
            if (index == tiles.Length)
                throw new TiledMapLoadException(
                    $"{context}: 'data' must contain exactly width * height ({cellCount}) elements."
                );

            if (!gidEl.TryGetUInt32(out var rawGid))
                throw new TiledMapLoadException(
                    $"{context}: 'data' elements must be non-negative integers."
                );

            // Clear Tiled's flip/rotation flags packed into the top 3 bits — flipped tiles
            // render as their unflipped base tile (flipping support is out of scope).
            var gid = rawGid & 0x1FFFFFFF;

            if (gid == 0)
            {
                tiles[index++] = Tilemap.EmptyTile;
                continue;
            }

            if (gid < (uint)firstGid)
                throw new TiledMapLoadException(
                    $"{context}: tile gid {gid} is less than the tileset's firstgid ({firstGid})."
                );

            tiles[index++] = (int)(gid - (uint)firstGid);
        }

        if (index != tiles.Length)
            throw new TiledMapLoadException(
                $"{context}: 'data' must contain exactly width * height ({cellCount}) elements."
            );

        var offsetXPx = GetOptionalFloat(layerEl, "offsetx") ?? 0f;
        var offsetYPx = GetOptionalFloat(layerEl, "offsety") ?? 0f;
        var scaleX = tileSizeWorldUnits.X / tileWidthPx;
        var scaleY = tileSizeWorldUnits.Y / tileHeightPx;

        var position = new Vector2(
            originVec.X + offsetXPx * scaleX,
            originVec.Y - offsetYPx * scaleY
        );

        Tilemap tilemap;
        try
        {
            tilemap = new Tilemap(tileset, layerWidth, layerHeight, tiles, tileSizeWorldUnits);
        }
        catch (ArgumentException ex)
        {
            throw new TiledMapLoadException(
                $"{context} produced an invalid Tilemap: {ex.Message}",
                ex
            );
        }

        List<Action<World, Entity>> adders =
        [
            (world, entity) => world.AddComponent(entity, new Transform2D(position)),
            (world, entity) => world.AddComponent(entity, tilemap),
            (world, entity) => world.AddComponent(entity, new RenderLayer(renderLayerIndex)),
        ];

        return new Scene.SceneEntityEntry(name, adders);
    }

    private static List<Scene.SceneEntityEntry> ParseObjectLayer(
        JsonElement layerEl,
        IReadOnlyDictionary<string, Prefab>? prefabsByName,
        int tileWidthPx,
        int tileHeightPx,
        int mapHeightPx,
        Vector2 tileSizeWorldUnits,
        Vector2 originVec
    )
    {
        var entries = new List<Scene.SceneEntityEntry>();

        if (!layerEl.TryGetProperty("objects", out var objectsEl))
            return entries;

        if (objectsEl.ValueKind != JsonValueKind.Array)
            throw new TiledMapLoadException(
                "Object layer 'objects' must be an array when present."
            );

        var scaleX = tileSizeWorldUnits.X / tileWidthPx;
        var scaleY = tileSizeWorldUnits.Y / tileHeightPx;

        foreach (var objectEl in objectsEl.EnumerateArray())
        {
            if (!(GetOptionalBool(objectEl, "visible") ?? true))
                continue;

            // Tiled 1.9+ renamed the object 'type' field to 'class'; accept either, preferring
            // 'class' when both are present.
            var prefabName =
                GetOptionalString(objectEl, "class") ?? GetOptionalString(objectEl, "type");
            if (string.IsNullOrEmpty(prefabName))
                continue;

            if (prefabsByName is null || !prefabsByName.TryGetValue(prefabName, out var prefab))
                continue;

            var x = GetRequiredFloat(objectEl, "x", "object");
            var y = GetRequiredFloat(objectEl, "y", "object");

            var position = new Vector2(
                originVec.X + x * scaleX,
                originVec.Y + (mapHeightPx - y) * scaleY
            );

            var name = GetOptionalString(objectEl, "name");

            List<Action<World, Entity>> adders =
            [
                (world, entity) =>
                {
                    prefab.Apply(world, entity);
                    world.AddComponent(entity, new Transform2D(position));
                },
            ];

            entries.Add(new Scene.SceneEntityEntry(name, adders));
        }

        return entries;
    }

    private static JsonElement GetRequiredArray(
        JsonElement element,
        string propertyName,
        string context
    )
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new TiledMapLoadException(
                $"{context} is missing required '{propertyName}' property."
            );

        if (property.ValueKind != JsonValueKind.Array)
            throw new TiledMapLoadException($"{context} '{propertyName}' must be an array.");

        return property;
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName,
        string context
    )
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new TiledMapLoadException(
                $"{context} is missing required '{propertyName}' property."
            );

        if (property.ValueKind != JsonValueKind.String)
            throw new TiledMapLoadException($"{context} '{propertyName}' must be a string.");

        return property.GetString() is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new TiledMapLoadException(
                $"{context} '{propertyName}' must be a non-empty string."
            );
    }

    private static int GetRequiredPositiveInt(
        JsonElement element,
        string propertyName,
        string context
    )
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new TiledMapLoadException(
                $"{context} is missing required '{propertyName}' property."
            );

        if (!property.TryGetInt32(out var value))
            throw new TiledMapLoadException($"{context} '{propertyName}' must be an integer.");

        if (value <= 0)
            throw new TiledMapLoadException($"{context} '{propertyName}' must be greater than 0.");

        return value;
    }

    private static float GetRequiredFloat(JsonElement element, string propertyName, string context)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new TiledMapLoadException(
                $"{context} is missing required '{propertyName}' property."
            );

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetSingle(out var value))
            throw new TiledMapLoadException($"{context} '{propertyName}' must be a number.");

        return value;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (
            !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
        )
            return null;

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static float? GetOptionalFloat(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out var value)
            ? value
            : null;
    }

    private static bool? GetOptionalBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
