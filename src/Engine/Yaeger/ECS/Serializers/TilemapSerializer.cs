using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Yaeger.Graphics;

namespace Yaeger.ECS.Serializers;

/// <summary>
/// Serializer for the <see cref="Tilemap"/> component.
/// </summary>
/// <remarks>
/// JSON format:
/// <code>
/// {
///   "type": "Tilemap",
///   "texturePath": "Assets/tiles.png",
///   "columns": 8,
///   "rows": 4,
///   "width": 4,
///   "height": 2,
///   "tileWidth": 1.0,
///   "tileHeight": 1.0,
///   "tiles": [0, 1, -1, 2, 3, 3, 3, 3],
///   "tint": [255, 255, 255, 255]
/// }
/// </code>
/// <c>rows</c> defaults to <c>1</c>, <c>tileWidth</c>/<c>tileHeight</c> default to <c>1.0</c>,
/// <c>tiles</c> defaults to an all-empty map, and <c>tint</c> defaults to white when absent.
/// The <c>tiles</c> array is row-major with row 0 at the top of the map; <c>-1</c> marks an
/// empty cell.
/// </remarks>
public sealed class TilemapSerializer : IComponentSerializer
{
    /// <inheritdoc/>
    public string TypeId => "Tilemap";

    /// <inheritdoc/>
    public Type? ComponentType => typeof(Tilemap);

    /// <inheritdoc/>
    public Action<World, Entity> Deserialize(JsonElement element)
    {
        var texturePath = GetRequiredString(element, "texturePath");
        var columns = GetRequiredPositiveInt(element, "columns");
        var rows = GetOptionalPositiveInt(element, "rows") ?? 1;
        var width = GetRequiredPositiveInt(element, "width");
        var height = GetRequiredPositiveInt(element, "height");
        var tileWidth = GetOptionalPositiveFloat(element, "tileWidth") ?? 1f;
        var tileHeight = GetOptionalPositiveFloat(element, "tileHeight") ?? 1f;

        int tileCount;
        try
        {
            tileCount = checked(columns * rows);
        }
        catch (OverflowException)
        {
            throw new PrefabLoadException(
                $"Tilemap 'columns' ({columns}) and 'rows' ({rows}) produce too many tiles."
            );
        }

        if ((long)width * height > int.MaxValue)
            throw new PrefabLoadException(
                $"Tilemap 'width' ({width}) and 'height' ({height}) produce too many cells."
            );
        var cellCount = width * height;

        int[]? tiles = null;
        if (element.TryGetProperty("tiles", out var tilesEl))
        {
            if (tilesEl.ValueKind != JsonValueKind.Array)
                throw new PrefabLoadException("Tilemap 'tiles' must be an array of integers.");

            tiles = new int[cellCount];
            var index = 0;
            foreach (var tileEl in tilesEl.EnumerateArray())
            {
                if (index == tiles.Length)
                    throw new PrefabLoadException(
                        $"Tilemap 'tiles' must contain exactly width * height ({cellCount}) elements."
                    );

                if (!tileEl.TryGetInt32(out var tileIndex))
                    throw new PrefabLoadException(
                        "Tilemap 'tiles' array elements must be integers."
                    );

                if (tileIndex < Tilemap.EmptyTile || tileIndex >= tileCount)
                    throw new PrefabLoadException(
                        $"Tilemap tile index {tileIndex} must be {Tilemap.EmptyTile} (empty) or within [0, {tileCount})."
                    );

                tiles[index++] = tileIndex;
            }

            if (index != tiles.Length)
                throw new PrefabLoadException(
                    $"Tilemap 'tiles' must contain exactly width * height ({cellCount}) elements."
                );
        }

        var tint = ComponentJson.GetOptionalColor(element, "tint", Color.White);
        var tileset = new Tileset(texturePath, columns, rows);
        var tileSize = new Vector2(tileWidth, tileHeight);

        var component = tiles is null
            ? new Tilemap(tileset, width, height, tileSize, tint)
            : new Tilemap(tileset, width, height, tiles, tileSize, tint);
        return (world, entity) => world.AddComponent(entity, component);
    }

    /// <inheritdoc/>
    public JsonNode? TrySerialize(World world, Entity entity)
    {
        if (!world.TryGetComponent<Tilemap>(entity, out var tilemap))
            return null;

        var obj = new JsonObject
        {
            ["type"] = TypeId,
            ["texturePath"] = tilemap.Tileset.TexturePath,
            ["columns"] = tilemap.Tileset.Columns,
            ["rows"] = tilemap.Tileset.Rows,
            ["width"] = tilemap.Width,
            ["height"] = tilemap.Height,
        };

        if (tilemap.TileSize.X != 1f)
            obj["tileWidth"] = tilemap.TileSize.X;
        if (tilemap.TileSize.Y != 1f)
            obj["tileHeight"] = tilemap.TileSize.Y;

        var tiles = new JsonArray();
        foreach (var tileIndex in tilemap.Tiles)
        {
            tiles.Add(tileIndex);
        }
        obj["tiles"] = tiles;

        if (
            tilemap.Tint.R != 255
            || tilemap.Tint.G != 255
            || tilemap.Tint.B != 255
            || tilemap.Tint.A != 255
        )
        {
            obj["tint"] = new JsonArray(
                tilemap.Tint.R,
                tilemap.Tint.G,
                tilemap.Tint.B,
                tilemap.Tint.A
            );
        }

        return obj;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new PrefabLoadException(
                $"Tilemap is missing required '{propertyName}' property."
            );

        if (property.ValueKind != JsonValueKind.String)
            throw new PrefabLoadException($"Tilemap '{propertyName}' must be a string.");

        return property.GetString() is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new PrefabLoadException(
                $"Tilemap '{propertyName}' must be a non-empty string."
            );
    }

    private static int GetRequiredPositiveInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            throw new PrefabLoadException(
                $"Tilemap is missing required '{propertyName}' property."
            );

        if (!property.TryGetInt32(out var value))
            throw new PrefabLoadException($"Tilemap '{propertyName}' must be an integer.");

        if (value <= 0)
            throw new PrefabLoadException($"Tilemap '{propertyName}' must be greater than 0.");

        return value;
    }

    private static int? GetOptionalPositiveInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (!property.TryGetInt32(out var value))
            throw new PrefabLoadException(
                $"Tilemap '{propertyName}' must be an integer when provided."
            );

        if (value <= 0)
            throw new PrefabLoadException(
                $"Tilemap '{propertyName}' must be greater than 0 when provided."
            );

        return value;
    }

    private static float? GetOptionalPositiveFloat(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind != JsonValueKind.Number)
            throw new PrefabLoadException(
                $"Tilemap '{propertyName}' must be a number when provided."
            );

        var value = (float)property.GetDouble();
        if (!float.IsFinite(value) || value <= 0)
            throw new PrefabLoadException(
                $"Tilemap '{propertyName}' must be a positive finite number when provided."
            );

        return value;
    }
}
